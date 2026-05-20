using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Runtime AssetBundle manager with three-tier loading (Base/Module/Hotfix).
/// Handles dependency resolution, reference counting, and bundle lifecycle.
/// 
/// Loading priority: Hotfix > Module > Base
/// </summary>
public class AssetBundleManager
{
    #region Singleton

    private static AssetBundleManager instance;
    public static AssetBundleManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new AssetBundleManager();
            }
            return instance;
        }
    }
    private AssetBundleManager() { }

    #endregion

    #region Fields

    /// <summary>
    /// Loaded AssetBundle cache: bundleName → LoadedBundle
    /// </summary>
    private Dictionary<string, LoadedBundle> loadedBundles = new Dictionary<string, LoadedBundle>();

    /// <summary>
    /// Enhanced manifest parsed from manifest.json.
    /// </summary>
    private AssetBundleManifest manifest;

    /// <summary>
    /// Base path for built-in bundles (StreamingAssets).
    /// </summary>
    private string builtInBundlePath;

    /// <summary>
    /// Base path for hot-updated bundles (persistentDataPath).
    /// </summary>
    private string hotUpdateBundlePath;

    private bool isInitialized = false;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize the AssetBundleManager. Must be called before any LoadAsset calls.
    /// </summary>
    /// <param name="hotUpdateDir">Subdirectory under persistentDataPath for hot update bundles</param>
    public void Initialize(string hotUpdateDir = "HotUpdate")
    {
        if (isInitialized)
        {
            Log.d("AssetBundleManager already initialized, skipping", "AssetBundleManager");
            return;
        }

        if (string.IsNullOrEmpty(hotUpdateDir))
            hotUpdateDir = "HotUpdate";

        if (string.IsNullOrEmpty(Application.streamingAssetsPath))
        {
            Log.w("Application.streamingAssetsPath is null, skipping AssetBundleManager init", "AssetBundleManager");
            return;
        }
        if (string.IsNullOrEmpty(Application.persistentDataPath))
        {
            Log.w("Application.persistentDataPath is null, skipping AssetBundleManager init", "AssetBundleManager");
            return;
        }

        builtInBundlePath = Path.Combine(Application.streamingAssetsPath, "HotUpdate", "assetbundles");
        hotUpdateBundlePath = Path.Combine(Application.persistentDataPath, hotUpdateDir);

        // Try to load enhanced manifest from hot update path first, then built-in
        manifest = LoadManifest(hotUpdateBundlePath) ?? LoadManifest(builtInBundlePath);

        if (manifest != null)
        {
            Log.d($"Manifest loaded: v{manifest.version}, {manifest.bundles.Count} bundles, platform={manifest.platform}",
                "AssetBundleManager");
        }
        else
        {
            Log.w("No manifest found, dependency resolution disabled", "AssetBundleManager");
        }

        isInitialized = true;
    }

    private AssetBundleManifest LoadManifest(string directory)
    {
        string manifestPath = Path.Combine(directory, "manifest.json");

        // On Android, StreamingAssets are inside the APK and cannot be read via File API.
        // Use UnityWebRequest for APK paths, File API for persistentDataPath.
        if (manifestPath.Contains("://") || manifestPath.StartsWith("jar:"))
        {
            return LoadManifestFromStreamingAssets(manifestPath);
        }

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(manifestPath);
            return JsonUtility.FromJson<AssetBundleManifest>(json);
        }
        catch (Exception e)
        {
            Log.w($"Failed to parse manifest at {manifestPath}: {e.Message}", "AssetBundleManager");
            return null;
        }
    }

    private AssetBundleManifest LoadManifestFromStreamingAssets(string uri)
    {
        try
        {
            using (var request = UnityEngine.Networking.UnityWebRequest.Get(uri))
            {
                var asyncOp = request.SendWebRequest();
                // Blocking wait — called during initialization, acceptable
                while (!asyncOp.isDone) { }

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler.text;
                    return JsonUtility.FromJson<AssetBundleManifest>(json);
                }
                else
                {
                    Log.w($"Failed to load manifest from StreamingAssets: {request.error}", "AssetBundleManager");
                }
            }
        }
        catch (Exception e)
        {
            Log.w($"Failed to load manifest from StreamingAssets: {e.Message}", "AssetBundleManager");
        }
        return null;
    }

    #endregion

    #region Asset Loading

    /// <summary>
    /// Load an asset of type T from a specific bundle.
    /// Automatically resolves and loads all dependencies.
    /// </summary>
    /// <param name="bundleName">Bundle name, e.g. "module_login_prefab.ab"</param>
    /// <param name="assetName">Asset name within the bundle</param>
    public T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
    {
        if (!isInitialized)
        {
            // Editor: AssetDatabase mode — AssetBundleManager not initialized.
            // ConfigManager.LoadFromAssetBundle will handle this and fall back.
            Log.d("AssetBundleManager not initialized — falling back to AssetDatabase", "AssetBundleManager");
            return null;
        }

        // Load the bundle (and its dependencies)
        AssetBundle bundle = LoadBundleWithDependencies(bundleName);
        if (bundle == null)
        {
            Log.w($"Failed to load bundle: {bundleName}", "AssetBundleManager");
            return null;
        }

        T asset = bundle.LoadAsset<T>(assetName);
        if (asset != null)
        {
            Log.d($"Asset loaded: {assetName} ({typeof(T).Name}) from {bundleName}", "AssetBundleManager");
        }
        else
        {
            Log.w($"Asset not found in bundle: {assetName} in {bundleName}", "AssetBundleManager");
        }

        return asset;
    }

    /// <summary>
    /// Load all assets of type T from a specific bundle.
    /// </summary>
    public T[] LoadAllAssets<T>(string bundleName) where T : UnityEngine.Object
    {
        if (!isInitialized)
        {
            Log.e("AssetBundleManager not initialized.", "AssetBundleManager");
            return null;
        }

        AssetBundle bundle = LoadBundleWithDependencies(bundleName);
        if (bundle == null)
        {
            return null;
        }

        return bundle.LoadAllAssets<T>();
    }

    #endregion

    #region Bundle Loading with Dependency Resolution

    /// <summary>
    /// Load a bundle and all its dependencies recursively.
    /// Loading priority: Hotfix > Module > Base (hot update overrides built-in).
    /// </summary>
    private AssetBundle LoadBundleWithDependencies(string bundleName)
    {
        // Load dependencies first
        if (manifest != null && manifest.dependencyGraph != null)
        {
            if (manifest.dependencyGraph.TryGetValue(bundleName, out List<string> deps))
            {
                foreach (string dep in deps)
                {
                    if (!loadedBundles.ContainsKey(dep))
                    {
                        LoadBundleInternal(dep);
                    }
                }
            }
        }

        // Load the requested bundle
        return LoadBundleInternal(bundleName);
    }

    /// <summary>
    /// Load a single bundle. Checks hot update path first, then built-in.
    /// </summary>
    private AssetBundle LoadBundleInternal(string bundleName)
    {
        // Already loaded?
        if (loadedBundles.TryGetValue(bundleName, out LoadedBundle existing))
        {
            existing.refCount++;
            return existing.bundle;
        }

        AssetBundle bundle = null;
        string sourcePath = null;

        // Priority 1: Hot update path (persistentDataPath)
        string hotUpdatePath = Path.Combine(hotUpdateBundlePath, bundleName);
        if (File.Exists(hotUpdatePath))
        {
            bundle = AssetBundle.LoadFromFile(hotUpdatePath);
            if (bundle != null)
            {
                sourcePath = hotUpdatePath;
                Log.d($"Bundle loaded from hot update: {bundleName}", "AssetBundleManager");
            }
        }

        // Priority 2: Built-in path (StreamingAssets)
        // On Android, StreamingAssets is inside APK — File.Exists fails but
        // AssetBundle.LoadFromFile handles the jar: URI internally.
        if (bundle == null)
        {
            string builtInPath = Path.Combine(builtInBundlePath, bundleName);
            bundle = AssetBundle.LoadFromFile(builtInPath);
            if (bundle != null)
            {
                sourcePath = builtInPath;
                Log.d($"Bundle loaded from built-in: {bundleName}", "AssetBundleManager");
            }
        }

        if (bundle != null)
        {
            loadedBundles[bundleName] = new LoadedBundle
            {
                bundle = bundle,
                refCount = 1,
                sourcePath = sourcePath,
                bundleName = bundleName
            };
            return bundle;
        }

        Log.w($"Bundle not found: {bundleName}", "AssetBundleManager");
        return null;
    }

    #endregion

    #region Bundle Unloading

    /// <summary>
    /// Unload a specific bundle. Decrements reference count.
    /// Bundle is actually unloaded when refCount reaches 0.
    /// </summary>
    /// <param name="bundleName">Bundle name</param>
    /// <param name="unloadAllLoadedObjects">If true, also destroys all loaded assets from this bundle</param>
    public void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
    {
        if (loadedBundles.TryGetValue(bundleName, out LoadedBundle loaded))
        {
            loaded.refCount--;
            if (loaded.refCount <= 0)
            {
                loaded.bundle.Unload(unloadAllLoadedObjects);
                loadedBundles.Remove(bundleName);
                Log.d($"Bundle unloaded: {bundleName}", "AssetBundleManager");
            }
        }
    }

    /// <summary>
    /// Unload all bundles. Optionally unload dependencies first.
    /// </summary>
    public void UnloadAllBundles(bool unloadAllLoadedObjects = false)
    {
        foreach (var kv in loadedBundles)
        {
            if (kv.Value.bundle != null)
            {
                kv.Value.bundle.Unload(unloadAllLoadedObjects);
            }
        }
        loadedBundles.Clear();
        Log.d("All bundles unloaded", "AssetBundleManager");
    }

    /// <summary>
    /// Unload bundles of a specific layer.
    /// </summary>
    public void UnloadBundlesByLayer(AssetBundleLayer layer, bool unloadAllLoadedObjects = false)
    {
        var toRemove = new List<string>();
        foreach (var kv in loadedBundles)
        {
            // Check manifest for layer info
            if (manifest != null)
            {
                var entry = manifest.bundles.Find(b => b.name == kv.Key);
                if (entry != null && entry.layer == layer)
                {
                    kv.Value.bundle.Unload(unloadAllLoadedObjects);
                    toRemove.Add(kv.Key);
                }
            }
        }

        foreach (string name in toRemove)
        {
            loadedBundles.Remove(name);
        }

        Log.d($"Unloaded {toRemove.Count} bundles from layer: {layer}", "AssetBundleManager");
    }

    #endregion

    #region Query

    /// <summary>
    /// Check if a bundle is currently loaded.
    /// </summary>
    public bool IsBundleLoaded(string bundleName)
    {
        return loadedBundles.ContainsKey(bundleName);
    }

    /// <summary>
    /// Set manifest directly from hot update data (manifest.json is not saved locally).
    /// </summary>
    public void SetManifest(AssetBundleManifest newManifest)
    {
        manifest = newManifest;
        if (manifest != null)
        {
            UnityEngine.Debug.Log($"[AssetBundleManager] Manifest set: v{manifest.version}, {manifest.bundles.Count} bundles");
        }
    }

    /// <summary>
    /// Reload manifest from hot update path after hot update completes.
    /// </summary>
    public void ReloadManifest()
    {
        try
        {
            UnityEngine.Debug.Log("[AssetBundleManager] ReloadManifest called");
            UnityEngine.Debug.Log($"[AssetBundleManager] hotUpdateBundlePath={hotUpdateBundlePath}");
            string manifestPath = Path.Combine(hotUpdateBundlePath, "manifest.json");
            UnityEngine.Debug.Log($"[AssetBundleManager] Checking manifest at: {manifestPath}, exists={File.Exists(manifestPath)}");
            manifest = LoadManifest(hotUpdateBundlePath) ?? manifest;
            if (manifest != null)
            {
                UnityEngine.Debug.Log($"[AssetBundleManager] Manifest reloaded: v{manifest.version}, {manifest.bundles.Count} bundles");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[AssetBundleManager] Manifest reload failed, manifest is still null");
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"[AssetBundleManager] ReloadManifest exception: {ex}");
        }
    }

    /// <summary>
    /// Get the loaded manifest.
    /// </summary>
    public AssetBundleManifest GetManifest()
    {
        return manifest;
    }

    /// <summary>
    /// Get all currently loaded bundle names.
    /// </summary>
    public List<string> GetLoadedBundleNames()
    {
        return new List<string>(loadedBundles.Keys);
    }

    /// <summary>
    /// Get the reference count for a bundle.
    /// </summary>
    public int GetBundleRefCount(string bundleName)
    {
        return loadedBundles.TryGetValue(bundleName, out LoadedBundle loaded) ? loaded.refCount : 0;
    }

    #endregion

    #region Internal Types

    private class LoadedBundle
    {
        public AssetBundle bundle;
        public int refCount;
        public string sourcePath;
        public string bundleName;
    }

    #endregion
}
