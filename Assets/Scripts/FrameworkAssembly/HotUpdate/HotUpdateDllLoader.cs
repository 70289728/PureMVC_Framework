using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Loads hot-updated C# assemblies via HybridCLR.
/// If HybridCLR is not installed, logs a clear error and skips DLL hot update.
/// </summary>
public class HotUpdateDllLoader
{
    private HotUpdateConfig config;

    // HybridCLR.RuntimeApi type name for reflection-based access
    private const string HybridCLRRuntimeApiType = "HybridCLR.RuntimeApi";
    private const string LoadMetadataMethod = "LoadMetadataForAOTAssembly";

    /// <summary>Hot update assembly file name (must match .asmdef name).</summary>
    public const string HotAssemblyDllName = "HotUpdateAssembly.dll";

    public HotUpdateDllLoader(HotUpdateConfig config)
    {
        this.config = config;
    }

    /// <summary>
    /// Check if HybridCLR is available in the project.
    /// </summary>
    public bool IsHybridCLRAvailable()
    {
        System.Type runtimeApi = System.Type.GetType(HybridCLRRuntimeApiType + ",HybridCLR.Runtime");
        if (runtimeApi == null)
        {
            // Try alternative assembly name
            runtimeApi = System.Type.GetType(HybridCLRRuntimeApiType + ",HybridCLR");
        }
        return runtimeApi != null;
    }

    /// <summary>
    /// Load AOT metadata DLLs required by HybridCLR.
    /// These should be in StreamingAssets or the hot update directory.
    /// </summary>
    public void LoadAOTMetadata()
    {
        if (!IsHybridCLRAvailable())
        {
            Log.w("HybridCLR not available, skipping AOT metadata load", "HotUpdateDllLoader");
            return;
        }

        // Editor doesn't need AOT metadata — HybridCLR only used in IL2CPP builds
#if UNITY_EDITOR
        return;
    }
#else
        string metadataDir = Path.Combine(Application.streamingAssetsPath, "HybridCLRMetadata");
        if (!Directory.Exists(metadataDir))
        {
            Log.w($"AOT metadata directory not found: {metadataDir}", "HotUpdateDllLoader");
            return;
        }

        var runtimeApiType = System.Type.GetType(HybridCLRRuntimeApiType + ",HybridCLR.Runtime")
                          ?? System.Type.GetType(HybridCLRRuntimeApiType + ",HybridCLR");

        var loadMethod = runtimeApiType?.GetMethod(LoadMetadataMethod, new[] { typeof(byte[]) });
        if (loadMethod == null)
        {
            Log.e("HybridCLR RuntimeApi.LoadMetadataForAOTAssembly method not found", "HotUpdateDllLoader");
            return;
        }

        string[] dllFiles = Directory.GetFiles(metadataDir, "*.dll");
        foreach (string dllPath in dllFiles)
        {
            try
            {
                byte[] dllBytes = File.ReadAllBytes(dllPath);
                int result = (int)loadMethod.Invoke(null, new object[] { dllBytes });
                Log.d($"Loaded AOT metadata: {Path.GetFileName(dllPath)} (result={result})", "HotUpdateDllLoader");
            }
            catch (System.Exception e)
            {
                Log.e($"Failed to load AOT metadata {Path.GetFileName(dllPath)}: {e.Message}", "HotUpdateDllLoader");
            }
        }
    }
#endif

    /// <summary>
    /// Read the hot-updated HotUpdateAssembly.dll bytes from persistentDataPath.
    /// Returns true if the DLL file exists and was read successfully.
    /// </summary>
    public bool ReadHotUpdateAssemblyBytes(out byte[] dllBytes)
    {
        dllBytes = null;
        string dllPath = GetHotAssemblyPersistentPath();
        if (!File.Exists(dllPath))
        {
            Log.d($"No hot update DLL found at: {dllPath}", "HotUpdateDllLoader");
            return false;
        }

        try
        {
            dllBytes = File.ReadAllBytes(dllPath);
            Log.d($"Read hot update DLL: {dllPath} ({dllBytes.Length} bytes)", "HotUpdateDllLoader");
            return true;
        }
        catch (System.Exception e)
        {
            Log.e($"Failed to read hot update DLL: {e.Message}", "HotUpdateDllLoader");
            return false;
        }
    }

    /// <summary>
    /// Get the full persistent path for the hot update assembly DLL.
    /// </summary>
    public string GetHotAssemblyPersistentPath()
    {
        return Path.Combine(Application.persistentDataPath, config.localHotUpdateDir, "dll", HotAssemblyDllName);
    }

    /// <summary>
    /// Get the full persistent path for the cached HotUpdateAssembly.dll (mirrored on first load).
    /// </summary>
    public string GetHotAssemblyCachePath()
    {
        return Path.Combine(Application.persistentDataPath, config.localHotUpdateDir, "cache", HotAssemblyDllName);
    }

    /// <summary>
    /// Load the hot-updated HotUpdateAssembly.dll from the persistent data path.
    /// </summary>
    /// <returns>True if the hot update DLL was loaded successfully</returns>
    public bool LoadHotUpdateAssembly()
    {
        // Editor doesn't support runtime assembly hot-reload — types already loaded anyway
#if UNITY_EDITOR
        return false;
    }
#else
        if (!IsHybridCLRAvailable())
        {
            Log.e("HybridCLR is not installed. C# hot update requires HybridCLR package.", "HotUpdateDllLoader");
            Log.e("Install via: https://hybridclr.doc.code-philosophy.com/docs/beginner/quickstart", "HotUpdateDllLoader");
            return false;
        }

        if (!ReadHotUpdateAssemblyBytes(out byte[] dllBytes))
            return false;

        try
        {
            Assembly hotAssembly = Assembly.Load(dllBytes);
            Log.d($"Hot update assembly loaded: {hotAssembly.FullName}", "HotUpdateDllLoader");

            // Persist a cache copy for next cold start
            SaveCacheCopy(dllBytes);

            return true;
        }
        catch (System.Exception e)
        {
            Log.e($"Failed to load hot update assembly: {e.Message}", "HotUpdateDllLoader");
            return false;
        }
    }
#endif
    /// <summary>
    /// Save a cache copy of the DLL so it can be loaded directly on next cold start
    /// without needing to unpack from Resources.
    /// </summary>
    private void SaveCacheCopy(byte[] dllBytes)
    {
        try
        {
            string cachePath = GetHotAssemblyCachePath();
            string cacheDir = Path.GetDirectoryName(cachePath);
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            File.WriteAllBytes(cachePath, dllBytes);
            Log.d($"Hot assembly cached to: {cachePath}", "HotUpdateDllLoader");
        }
        catch (System.Exception e)
        {
            Log.w($"Failed to cache hot assembly: {e.Message}", "HotUpdateDllLoader");
        }
    }
}
