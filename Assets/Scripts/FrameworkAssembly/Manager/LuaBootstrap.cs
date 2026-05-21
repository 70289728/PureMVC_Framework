using UnityEngine;

/// <summary>
/// Lua subsystem bootstrap. Attached to a GameObject by GameMain.InitModule.
/// Drives LuaEnv.Tick() every frame and provides a clean entry point for Lua scripting.
///
/// Script paths:
///   Built-in:    LuaScripts/          (packaged in APK via StreamingAssets)
///   Hot-update:  persistentDataPath/HotUpdate/LuaScripts/  (downloaded)
///
/// Usage:
///   LuaBootstrap.Instance.Require("LuaScripts.main")                — require main.lua
///   LuaBootstrap.Instance.Require("LuaScripts.HotUpdate.ui")          — require hot-updated module
///   LuaBootstrap.Instance.Call("LuaScripts/BuiltIn/main.lua.txt", "OnInit")  — call a function
/// </summary>
public class LuaBootstrap : MonoBehaviour
{
    #region Singleton

    private static LuaBootstrap instance;
    public static LuaBootstrap Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("LuaBootstrap");
                instance = go.AddComponent<LuaBootstrap>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    #endregion

    private HotUpdateLuaLoader luaLoader;
    private bool initialized = false;

    /// <summary>Calls LuaEnv.FullGc every N ticks.</summary>
    [SerializeField] private int gcInterval = 100;
    private int tickCount = 0;

    public bool IsInitialized => initialized;
    public HotUpdateLuaLoader Loader => luaLoader;

    /// <summary>
    /// Initialize Lua subsystem. Called by GameMain.InitModule after hot update completes.
    /// Safe to call multiple times.
    /// </summary>
    public void Initialize()
    {
        if (initialized)
            return;

        var hotUpdateMgr = HotUpdateManager.Instance;
        var lua = hotUpdateMgr.LuaLoader as HotUpdateLuaLoader;
        if (lua == null)
        {
            Log.e("HotUpdateLuaLoader not available, Lua subsystem disabled", "LuaBootstrap");
            return;
        }

        luaLoader = lua;
        initialized = true;
        Log.d("Lua subsystem initialized", "LuaBootstrap");

        // Auto-execute main entry script via require
        Require("LuaScripts.main");
    }

    private void Update()
    {
        if (!initialized) return;

        luaLoader.Tick();
        tickCount++;

        if (tickCount >= gcInterval)
        {
            tickCount = 0;
            luaLoader.FullGc();
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (!initialized) return;
        luaLoader.SafeDoString(
            pause ? "if GameMain and GameMain.OnApplicationPause then GameMain:OnApplicationPause(true) end" : "if GameMain and GameMain.OnApplicationPause then GameMain:OnApplicationPause(false) end",
            "OnApplicationPause");
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!initialized) return;
        luaLoader.SafeDoString(
            focus ? "if GameMain and GameMain.OnApplicationFocusIn then GameMain:OnApplicationFocusIn() end" : "if GameMain and GameMain.OnApplicationFocusOut then GameMain:OnApplicationFocusOut() end",
            "OnApplicationFocus");
    }

    private void OnDestroy()
    {
        // Dispose LuaEnv via HotUpdateLuaLoader to release native resources properly.
        // Previously relied on GC finalizer — explicit Dispose is safer, especially on IL2CPP.
        if (luaLoader != null)
        {
            luaLoader.Dispose();
            luaLoader = null;
            Log.d("LuaEnv disposed", "LuaBootstrap");
        }
        initialized = false;
        instance = null;
    }

    // ──────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────

    /// <summary>
    /// Require a Lua module. The module path uses dot notation, mapped to file paths.
    /// e.g. Require("LuaScripts.BuiltIn.main") → LuaScripts/BuiltIn/main.lua.txt.
    /// e.g. Require("LuaScripts.HotUpdate.ui") → LuaScripts/HotUpdate/ui.lua.txt.
    /// </summary>
    public bool Require(string modulePath)
    {
        if (!EnsureReady()) return false;
        return luaLoader.Require(modulePath);
    }

    /// <summary>
    /// Execute a Lua script by relative path.
    /// e.g. Execute("LuaScripts/BuiltIn/main.lua.txt").
    /// </summary>
    public bool Execute(string path)
    {
        if (!EnsureReady()) return false;
        return luaLoader.ExecuteScript(path);
    }

    /// <summary>
    /// Call a named function in a Lua script.
    /// </summary>
    public object Call(string path, string funcName, params object[] args)
    {
        if (!EnsureReady()) return null;
        return luaLoader.CallFunction(path, funcName, args);
    }

    /// <summary>
    /// Execute raw Lua code safely.
    /// </summary>
    public bool DoString(string scriptContent, string chunkName = "inline")
    {
        if (!EnsureReady()) return false;
        return luaLoader.SafeDoString(scriptContent, chunkName);
    }

    // ──────────────────────────────────────────────
    //  Restart prompt
    // ──────────────────────────────────────────────

    /// <summary>
    /// Prompt user to restart after Lua hot update.
    /// Called by HotUpdateManager after detecting .lua.enc downloads.
    /// In production, this can show a UI dialog with "Restart Now" / "Later".
    /// </summary>
    public void PromptRestart()
    {
        Log.d("Lua scripts updated, notify Lua layer to prompt restart", "LuaBootstrap");
        // Notify Lua so it can show a UI dialog
        luaLoader.SafeDoString(
            "if GameMain and GameMain.OnLuaUpdateComplete then GameMain:OnLuaUpdateComplete() end",
            "OnLuaUpdateComplete");
    }

    // ──────────────────────────────────────────────
    //  Internal
    // ──────────────────────────────────────────────

    /// <summary>
    /// Reload all Lua modules after hot update downloads new .lua.enc files.
    /// Called by HotUpdateManager after ClearCache.
    /// </summary>
    public void ReloadAfterHotUpdate()
    {
        if (!initialized) return;
        Require("LuaScripts.main");
        Log.d("Lua modules reloaded after hot update", "LuaBootstrap");
    }

    private bool EnsureReady()
    {
        if (!initialized)
        {
            Log.w("LuaBootstrap not initialized", "LuaBootstrap");
            return false;
        }
        return true;
    }
}
