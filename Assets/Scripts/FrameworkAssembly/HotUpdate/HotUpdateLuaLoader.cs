using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// xLua-based Lua loader implementing ILuaLoader.
/// All xLua types accessed via reflection (xLua in Assembly-CSharp, no .asmdef).
/// 
/// File format: .lua (plain) or .lua.enc (AES-128-CBC encrypted).
/// Loading priority: customLoader > persistentDataPath > StreamingAssets > Editor Assets.
/// 
/// Must call Tick() every frame from a MonoBehaviour.Update.
/// </summary>
public class HotUpdateLuaLoader : ILuaLoader, IDisposable
{
    // ── Config ──
    private HotUpdateConfig config;

    // ── xLua Reflection ──
    private object luaEnv;
    private Type luaEnvType;
    private Type luaFunctionType;
    private Type luaTableType;
    private MethodInfo globalGetProperty;
    private MethodInfo luaTableGetTMethod;
    private MethodInfo luaFunctionCallMethod;
    private MethodInfo luaFunctionDisposeMethod;
    private MethodInfo doStringTextMethod;
    private MethodInfo tickMethod;
    private MethodInfo fullGcMethod;
    private PropertyInfo memoryProperty;
    private MethodInfo disposeMethod;

    // ── Delegates ──
    public delegate byte[] CustomLoaderDelegate(ref string filepath);
    private CustomLoaderDelegate customLoader;

    // ── State ──
    private readonly Dictionary<string, string> textCache = new Dictionary<string, string>();
    private readonly Dictionary<string, string> moduleNameToPath = new Dictionary<string, string>();
    private bool disposed;

    // ── Lua Security ──
    // Expected SHA256 hashes for hot-updated Lua files (key = relative path, value = hex hash).
    // Populated by HotUpdateManager after downloading and verifying the Lua bundle manifest.
    // Phase 2: manifest itself will be RSA-signed.
    private static readonly Dictionary<string, string> _expectedHashes = new Dictionary<string, string>();

    /// <summary>
    /// Set the expected SHA256 hashes for Lua file integrity verification.
    /// Called by HotUpdateManager after downloading the Lua bundle.
    /// </summary>
    public static void SetExpectedHashes(Dictionary<string, string> hashes)
    {
        lock (_expectedHashes)
        {
            _expectedHashes.Clear();
            if (hashes != null)
            {
                foreach (var kv in hashes)
                    _expectedHashes[kv.Key] = kv.Value;
            }
        }
        Log.d($"Lua expected hashes set: {_expectedHashes.Count} entries", "HotUpdateLuaLoader");
    }

    // ──────────────────────────────────────────────
    //  Construction & Reflection Init
    // ──────────────────────────────────────────────

    public HotUpdateLuaLoader(HotUpdateConfig config)
    {
        this.config = config;
        InitReflection();
        Log.d("HotUpdateLuaLoader initialized with xLua", "HotUpdateLuaLoader");
    }

    private void InitReflection()
    {
        luaEnvType = Type.GetType("XLua.LuaEnv,Assembly-CSharp");
        if (luaEnvType == null)
        {
            Log.e("XLua.LuaEnv not found. Run XLua/Generate Code first.", "HotUpdateLuaLoader");
            return;
        }

        luaEnv = Activator.CreateInstance(luaEnvType);
        luaFunctionType = Type.GetType("XLua.LuaFunction,Assembly-CSharp");
        luaTableType = Type.GetType("XLua.LuaTable,Assembly-CSharp");

        // Resolve MethodInfo once
        doStringTextMethod = luaEnvType.GetMethod("DoString",
            new[] { typeof(string), typeof(string), luaTableType ?? typeof(object) })
            ?? luaEnvType.GetMethod("DoString", new[] { typeof(string), typeof(string) });

        globalGetProperty = luaEnvType.GetProperty("Global")?.GetMethod;

        if (luaTableType != null)
        {
            // LuaTable.Get<TValue>(string key) — 1 generic param, 1 param (string)
            foreach (var m in luaTableType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name == "Get" && m.IsGenericMethod && m.GetGenericArguments().Length == 1
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string))
                {
                    luaTableGetTMethod = m;
                    break;
                }
            }
            if (luaTableGetTMethod == null)
                Log.w("LuaTable.Get<TValue>(string) not found", "HotUpdateLuaLoader");
        }

        if (luaFunctionType != null)
        {
            luaFunctionCallMethod = luaFunctionType.GetMethod("Call", new[] { typeof(object[]) });
            luaFunctionDisposeMethod = luaFunctionType.GetMethod("Dispose", Type.EmptyTypes);
        }

        tickMethod = luaEnvType.GetMethod("Tick", Type.EmptyTypes);
        fullGcMethod = luaEnvType.GetMethod("FullGc", Type.EmptyTypes);
        memoryProperty = luaEnvType.GetProperty("Memroy"); // xLua typo: 'Memroy' not 'Memory'
        disposeMethod = luaEnvType.GetMethod("Dispose", Type.EmptyTypes);

        // Register file loader by injecting a C# function into Lua's package.searchers.
        // This avoids cross-assembly delegate binding issues with AddLoader.
        InjectPackageSearcher();
    }

    /// <summary>
    /// Insert a Lua-callable C# function at the front of package.searchers
    /// so xLua's require() will try our file loader first.
    /// </summary>
    private void InjectPackageSearcher()
    {
        string initCode = @"
            local _hotloader = function(path, filename)
                local ok, bytes = pcall(_CS_FILE_LOADER, path)
                if ok and bytes ~= nil then
                    return load(bytes, filename)
                end
                return nil, 'not found'
            end
            table.insert(package.searchers, 1, _hotloader)
        ";
        DoStringInvoke(initCode, "inject_searcher");

        // Now register _CS_FILE_LOADER as a global Lua function backed by C#
        var globalObj = globalGetProperty?.Invoke(luaEnv, null);
        if (globalObj == null) return;

        // LuaTable.Set<TKey,TValue>(TKey, TValue) — 2 generic params, 2 method params.
        MethodInfo setMethod = null;
        foreach (var m in luaTableType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.Name == "Set" && m.IsGenericMethod && m.GetGenericArguments().Length == 2
                && m.GetParameters().Length == 2)
            {
                setMethod = m;
                break;
            }
        }
        if (setMethod != null)
        {
            try
            {
                // Wrap the loader in a Func<string, object> that Lua can call
                Func<string, object> csLoader = (path) =>
                {
                    byte[] bytes = LoadModuleBytes(path);
                    return bytes;  // Lua receives the byte[] as a Lua string
                };
                var closedSet = setMethod.MakeGenericMethod(typeof(string), typeof(object));
                closedSet.Invoke(globalObj, new object[] { "_CS_FILE_LOADER", csLoader });
                Log.d("package.searchers injected with C# loader", "HotUpdateLuaLoader");
            }
            catch (Exception e)
            {
                Log.w($"Failed to inject package.searchers: {e.Message}", "HotUpdateLuaLoader");
            }
        }
        else
        {
            Log.w("Failed to inject package.searchers — LuaTable.Set<T> not found", "HotUpdateLuaLoader");
        }
    }

    // ──────────────────────────────────────────────
    //  ILuaLoader
    // ──────────────────────────────────────────────

    /// <summary>
    /// File loader: customLoader > persistentDataPath (.lua / .lua.enc) > StreamingAssets (.lua.enc) > Editor Assets.
    /// Supports both plain .lua (Editor fallback) and encrypted .lua.enc (production).
    /// </summary>
    private byte[] LoadModuleBytes(string modulePath)
    {
        string fp = modulePath;
        var bytes = customLoader?.Invoke(ref fp);
        if (bytes != null) return TryDecryptIfNeeded(fp, bytes);

        string relativePath = modulePath.Replace('.', '/');

        // Priority 1: persistentDataPath (hot-updated, may be .lua.enc or .lua)
        string pp = Path.Combine(Application.persistentDataPath, config.localHotUpdateDir, relativePath);
        bytes = TryLoadFile(pp);
        if (bytes != null) return bytes;

        // Priority 2: StreamingAssets (built-in, encrypted .lua.enc)
        string sa = Path.Combine(Application.streamingAssetsPath, relativePath);
        bytes = TryLoadFile(sa);
        if (bytes != null) return bytes;

        // Priority 3 (Editor only): project Assets folder (plain .lua)
        string projectPath = Path.Combine(Application.dataPath, relativePath + ".lua");
        if (File.Exists(projectPath)) return File.ReadAllBytes(projectPath);

        return null;
    }

    /// <summary>
    /// Try to load {path}.lua.enc first, then {path}.lua. Auto-decrypt .lua.enc.
    /// For files loaded from persistentDataPath, verifies SHA256 against expected hashes.
    /// </summary>
    private static byte[] TryLoadFile(string basePath)
    {
        bool isHotUpdate = basePath.Contains(Application.persistentDataPath);

        // Try encrypted first
        string encPath = basePath + ".lua.enc";
        if (basePath.Contains("://") || basePath.StartsWith("jar:"))
        {
            byte[] raw = ReadStreamingAssetsSync(encPath);
            if (raw != null) return AesHelper.Decrypt(raw);
            // Also try .lua
            raw = ReadStreamingAssetsSync(basePath + ".lua");
            return raw;
        }

        if (File.Exists(encPath))
        {
            byte[] raw = File.ReadAllBytes(encPath);
            if (isHotUpdate && !VerifyLuaHash(encPath, raw))
            {
                Log.e($"Lua file integrity check FAILED: {encPath}", "HotUpdateLuaLoader");
                return null;
            }
            return AesHelper.Decrypt(raw);
        }

        string luaPath = basePath + ".lua";
        if (File.Exists(luaPath))
        {
            byte[] raw = File.ReadAllBytes(luaPath);
            if (isHotUpdate && !VerifyLuaHash(luaPath, raw))
            {
                Log.e($"Lua file integrity check FAILED: {luaPath}", "HotUpdateLuaLoader");
                return null;
            }
            return raw;
        }

        return null;
    }

    /// <summary>
    /// Verify the SHA256 of a Lua file against the expected hash.
    /// If no expected hash is registered for this path, skip verification.
    /// Returns true if verified or skipped, false if hash mismatch.
    /// </summary>
    private static bool VerifyLuaHash(string filePath, byte[] data)
    {
        string relativeName = Path.GetFileName(filePath);
        string expectedHash = null;
        bool hasEntry = false;

        lock (_expectedHashes)
        {
            hasEntry = _expectedHashes.TryGetValue(relativeName, out expectedHash);
        }

        if (!hasEntry)
            return true; // No expected hash registered — skip verification

        using (var sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(data);
            string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            bool match = string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
            if (!match)
            {
                Log.e($"Lua hash mismatch: {relativeName}\n  Expected: {expectedHash}\n  Actual:   {actualHash}",
                    "HotUpdateLuaLoader");
            }
            return match;
        }
    }

    /// <summary>
    /// If the file ends with .lua.enc, decrypt it.
    /// </summary>
    private static byte[] TryDecryptIfNeeded(string path, byte[] data)
    {
        if (path.EndsWith(".lua.enc", StringComparison.OrdinalIgnoreCase))
            return AesHelper.Decrypt(data);
        return data;
    }

    // ──────────────────────────────────────────────
    //  ILuaLoader
    // ──────────────────────────────────────────────

    public string LoadScript(string path) => TryLoadScriptText(path, out string t) ? t : null;

    public bool ExecuteScript(string path)
    {
        if (!TryLoadScriptText(path, out string t)) return false;
        return DoStringInvoke(t, path);
    }

    public object CallFunction(string path, string funcName, params object[] args)
    {
        if (!textCache.TryGetValue(path, out string text))
        {
            if (!TryLoadScriptText(path, out text)) return null;
            textCache[path] = text;
        }
        return SafeCallFunction(text, path, funcName, args);
    }

    // ──────────────────────────────────────────────
    //  Core API
    // ──────────────────────────────────────────────

    public bool Require(string modulePath)
    {
        if (luaEnv == null) return false;

        // Load file directly via C# — bypass xLua's require/CustomLoader/package.searchers
        // which fails with cross-assembly delegate binding on IL2CPP.
        byte[] bytes = LoadModuleBytes(modulePath);
        if (bytes == null)
        {
            Log.w($"Lua module not found: {modulePath}", "HotUpdateLuaLoader");
            return false;
        }

        string code = System.Text.Encoding.UTF8.GetString(bytes);

        // Check package.loaded to avoid double-execution (mimic require behavior)
        string wrapped = string.Format(@"
            if package.loaded['{0}'] ~= nil then return end
            package.loaded['{0}'] = true
            local ok, err = pcall(load({1}, '{0}'))
            if not ok then error(err, 0) end
        ", modulePath, EscapeLuaString(code));

        return DoStringInvoke(wrapped, modulePath);
    }

    /// <summary>Escape Lua string for embedding in Lua code.</summary>
    private static string EscapeLuaString(string s)
    {
        return "[[" + s.Replace("]]", "]=]") + "]]";
    }

    public bool SafeDoString(string code, string chunkName = "lua")
        => DoStringInvoke(code, chunkName);

    public void AddCustomLoader(CustomLoaderDelegate loader) => customLoader = loader;

    public void Tick()
    {
        if (luaEnv != null && !disposed) tickMethod?.Invoke(luaEnv, null);
    }

    public void FullGc()
    {
        if (luaEnv != null && !disposed) fullGcMethod?.Invoke(luaEnv, null);
    }

    /// <summary>
    /// Current Lua memory usage in KB (via LuaEnv.Memroy).
    /// Returns -1 if unavailable.
    /// </summary>
    public int MemoryKB
    {
        get
        {
            if (luaEnv == null || disposed || memoryProperty == null) return -1;
            try { return (int)(long)memoryProperty.GetValue(luaEnv); }
            catch { return -1; }
        }
    }

    public void ClearCache()
    {
        textCache.Clear();
        moduleNameToPath.Clear();
        // Clear Lua package.loaded so next require reloads from disk
        DoStringInvoke("for k in pairs(package.loaded) do if type(k) == 'string' and k:find('LuaScripts') then package.loaded[k] = nil end end", "clear_package_loaded");
        Log.d("Lua cache cleared", "HotUpdateLuaLoader");
    }

    public bool RegisterModule(string path, string globalName)
    {
        if (!TryLoadScriptText(path, out string text)) return false;
        if (!DoStringInvoke(text, path)) return false;
        try
        {
            var global = globalGetProperty?.Invoke(luaEnv, null);
            if (global == null) return false;
            var getMethod = luaTableGetTMethod?.MakeGenericMethod(luaTableType);
            var moduleTable = getMethod?.Invoke(global, new object[] { globalName });
            if (moduleTable == null) return false;
            // LuaTable.Set<TKey,TValue>(TKey, TValue) — 2 generic params
            MethodInfo setGenericMethod = null;
            foreach (var m in luaTableType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name == "Set" && m.IsGenericMethod && m.GetGenericArguments().Length == 2
                    && m.GetParameters().Length == 2)
                {
                    setGenericMethod = m;
                    break;
                }
            }
            if (setGenericMethod != null)
                setGenericMethod.MakeGenericMethod(typeof(string), luaTableType).Invoke(global, new object[] { globalName, moduleTable });
            moduleNameToPath[globalName] = path;
            return true;
        }
        catch (Exception e)
        {
            Log.e($"RegisterModule failed [{globalName}]: {e.Message}", "HotUpdateLuaLoader");
            return false;
        }
    }

    // ──────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────

    private bool DoStringInvoke(string code, string chunkName)
    {
        if (doStringTextMethod == null || luaEnv == null)
        {
            Log.e($"DoString unavailable: method={doStringTextMethod != null}, env={luaEnv != null}", "HotUpdateLuaLoader");
            return false;
        }
        try
        {
            var n = doStringTextMethod.GetParameters().Length;
            if (n >= 3) doStringTextMethod.Invoke(luaEnv, new object[] { code, chunkName, null });
            else if (n == 2) doStringTextMethod.Invoke(luaEnv, new object[] { code, chunkName });
            else doStringTextMethod.Invoke(luaEnv, new object[] { code });
            return true;
        }
        catch (TargetInvocationException tie)
        {
            Log.e($"Lua error [{chunkName}]: {(tie.InnerException?.Message ?? tie.Message)}", "HotUpdateLuaLoader");
            return false;
        }
        catch (Exception e)
        {
            Log.e($"Lua error [{chunkName}]: {e.Message}", "HotUpdateLuaLoader");
            return false;
        }
    }

    private object SafeCallFunction(string text, string chunkName, string funcName, object[] args)
    {
        if (luaEnv == null || luaFunctionType == null) return null;
        try
        {
            if (!DoStringInvoke(text, chunkName)) return null;
            var global = globalGetProperty?.Invoke(luaEnv, null);
            if (global == null) return null;
            var getMethod = luaTableGetTMethod?.MakeGenericMethod(luaFunctionType);
            var funcObj = getMethod?.Invoke(global, new object[] { funcName });
            if (funcObj == null)
            {
                Log.w($"Lua function not found: {funcName} in {chunkName}", "HotUpdateLuaLoader");
                return null;
            }
            object result = luaFunctionCallMethod?.Invoke(funcObj, new object[] { args ?? new object[0] });
            luaFunctionDisposeMethod?.Invoke(funcObj, null);
            return result;
        }
        catch (Exception e)
        {
            Log.e($"CallFunction [{chunkName}.{funcName}]: {e.Message}", "HotUpdateLuaLoader");
            return null;
        }
    }

    private bool TryLoadScriptText(string path, out string text)
    {
        text = null;
        string pp = Path.Combine(Application.persistentDataPath, config.localHotUpdateDir, path);
        if (File.Exists(pp)) { text = File.ReadAllText(pp); return true; }
        string sa = Path.Combine(Application.streamingAssetsPath, "LuaScripts", path);
        if (sa.Contains("://") || sa.StartsWith("jar:"))
        {
            byte[] raw = ReadStreamingAssetsSync(sa);
            if (raw != null) { text = System.Text.Encoding.UTF8.GetString(raw); return true; }
        }
        else if (File.Exists(sa)) { text = File.ReadAllText(sa); return true; }
        Log.w($"Lua script not found: {path}", "HotUpdateLuaLoader");
        return false;
    }

    /// <summary>
    /// Synchronously read a file from StreamingAssets using UnityWebRequest.
    /// Only use for small files (Lua scripts) during initialization.
    /// </summary>
    private static byte[] ReadStreamingAssetsSync(string uri)
    {
        using (var request = UnityWebRequest.Get(uri))
        {
            var op = request.SendWebRequest();
            while (!op.isDone) { }
            if (request.result == UnityWebRequest.Result.Success)
                return request.downloadHandler.data;
            Log.w($"Failed to read StreamingAssets: {uri} — {request.error}", "HotUpdateLuaLoader");
            return null;
        }
    }

    // ──────────────────────────────────────────────
    //  IDisposable
    // ──────────────────────────────────────────────

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        textCache.Clear();
        moduleNameToPath.Clear();
        customLoader = null;
        if (luaEnv != null) { disposeMethod?.Invoke(luaEnv, null); luaEnv = null; }
        Log.d("HotUpdateLuaLoader disposed", "HotUpdateLuaLoader");
    }
}
