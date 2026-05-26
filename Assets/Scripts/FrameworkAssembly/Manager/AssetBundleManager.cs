using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
    private static readonly object _instanceLock = new object();
    public static AssetBundleManager Instance
    {
        get
        {
            if (instance == null)
            {
                lock (_instanceLock)
                {
                    if (instance == null)
                        instance = new AssetBundleManager();
                }
            }
            return instance;
        }
    }
    private AssetBundleManager() { }

    #endregion

    #region Fields

    /// <summary>
    /// Loaded AssetBundle cache: bundleName -> LoadedBundle.
    /// ConcurrentDictionary for thread-safe Add/Remove; refCount uses Interlocked.
    /// </summary>
    private readonly ConcurrentDictionary<string, LoadedBundle> loadedBundles = new ConcurrentDictionary<string, LoadedBundle>();

    /// <summary>
    /// Lock for serializing bundle load operations (AssetBundle.LoadFromFile is not thread-safe).
    /// </summary>
    private readonly object _loadLock = new object();

    /// <summary>
    /// Enhanced manifest parsed from manifest.json. volatile for cross-thread visibility.
    /// </summary>
    private volatile AssetBundleManifest manifest;

    /// <summary>
    /// Base path for built-in bundles (StreamingAssets).
    /// </summary>
    private string builtInBundlePath;

    /// <summary>
    /// Base path for hot-updated bundles (persistentDataPath).
    /// </summary>
    private string hotUpdateBundlePath;

    private volatile bool isInitialized = false;

    #endregion

    #region Initialization

    /// <summary>
    /// <summary>
    /// Initialize the AssetBundleManager. Single coroutine entry point — must be awaited
    /// (yield return) before any LoadAsset call.
    ///
    /// Internal platform branching:
    ///   - PersistentDataPath manifest:        File API (all platforms — regular files, fast)
    ///   - StreamingAssets manifest on Android: UnityWebRequest (jar URI inside APK)
    ///   - StreamingAssets manifest elsewhere:  File API (regular path, fast)
    /// </summary>
    /// <param name="hotUpdateDir">Subdirectory under persistentDataPath for hot update bundles</param>
    public IEnumerator InitializeCoroutine(string hotUpdateDir = "HotUpdate")
    {
        if (!PrepareInitPaths(hotUpdateDir))
            yield break;

        // Priority 1: persistentDataPath — always regular file IO, all platforms
        manifest = LoadManifestFromFile(hotUpdateBundlePath);

        // Priority 2: built-in StreamingAssets — branch on platform
        if (manifest == null)
        {
            string builtInManifestPath = Path.Combine(builtInBundlePath, "manifest.json");
            if (IsAndroidStreamingPath(builtInManifestPath))
            {
                // Android APK: must use UnityWebRequest (jar URI)
                AssetBundleManifest loaded = null;
                yield return LoadManifestFromStreamingAssetsCoroutine(builtInManifestPath, m => loaded = m);
                manifest = loaded;
            }
            else
            {
                // PC / iOS / Editor: regular file IO, zero async overhead
                manifest = LoadManifestFromFile(builtInBundlePath);
            }
        }

        FinalizeInit();
    }

    private bool PrepareInitPaths(string hotUpdateDir)
    {
        if (isInitialized)
        {
            Log.d("AssetBundleManager already initialized, skipping", "AssetBundleManager");
            return false;
        }

        if (string.IsNullOrEmpty(hotUpdateDir))
            hotUpdateDir = "HotUpdate";

        if (string.IsNullOrEmpty(Application.streamingAssetsPath))
        {
            Log.w("Application.streamingAssetsPath is null, skipping AssetBundleManager init", "AssetBundleManager");
            return false;
        }
        if (string.IsNullOrEmpty(Application.persistentDataPath))
        {
            Log.w("Application.persistentDataPath is null, skipping AssetBundleManager init", "AssetBundleManager");
            return false;
        }

        builtInBundlePath = Path.Combine(Application.streamingAssetsPath, "HotUpdate", "assetbundles");
        hotUpdateBundlePath = Path.Combine(Application.persistentDataPath, hotUpdateDir);
        return true;
    }

    private void FinalizeInit()
    {
        if (manifest != null)
        {
            Log.d($"Manifest loaded: v{manifest.version}, {manifest.bundles.Count} bundles, platform={manifest.platform}", "AssetBundleManager");
        }
        else
        {
            Log.w("No manifest found, dependency resolution disabled", "AssetBundleManager");
        }
        isInitialized = true;
    }

    private static bool IsAndroidStreamingPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        return path.Contains("://") || path.StartsWith("jar:");
    }

    private AssetBundleManifest LoadManifestFromFile(string directory)
    {
        string manifestPath = Path.Combine(directory, "manifest.json");
        if (IsAndroidStreamingPath(manifestPath)) return null;
        if (!File.Exists(manifestPath)) return null;

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

    /// <summary>
    /// Coroutine: load manifest from APK StreamingAssets via UnityWebRequest.
    /// Result is delivered via callback to avoid blocking the main thread.
    /// </summary>
    private IEnumerator LoadManifestFromStreamingAssetsCoroutine(string uri, Action<AssetBundleManifest> onLoaded)
    {
        AssetBundleManifest result = null;
        UnityEngine.Networking.UnityWebRequest request = null;
        try
        {
            request = UnityEngine.Networking.UnityWebRequest.Get(uri);
        }
        catch (Exception e)
        {
            Log.w($"Failed to create UWR for manifest: {e.Message}", "AssetBundleManager");
            onLoaded?.Invoke(null);
            yield break;
        }

        using (request)
        {
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    string json = request.downloadHandler.text;
                    result = JsonUtility.FromJson<AssetBundleManifest>(json);
                }
                catch (Exception e)
                {
                    Log.w($"Failed to parse manifest from StreamingAssets: {e.Message}", "AssetBundleManager");
                }
            }
            else
            {
                Log.w($"Failed to load manifest from StreamingAssets: {request.error}", "AssetBundleManager");
            }
        }

        onLoaded?.Invoke(result);
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
        // Snapshot manifest to a local var for thread-safe read
        var m = manifest;
        if (m != null && m.dependencyGraph != null)
        {
            if (m.dependencyGraph.TryGetValue(bundleName, out List<string> deps))
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
    /// Thread-safe: uses _loadLock to serialize AssetBundle.LoadFromFile + cache insertion.
    /// </summary>
    private AssetBundle LoadBundleInternal(string bundleName)
    {
        // Already loaded? Increment refCount atomically.
        if (loadedBundles.TryGetValue(bundleName, out LoadedBundle existing))
        {
            Interlocked.Increment(ref existing.refCount);
            return existing.bundle;
        }

        lock (_loadLock)
        {
            // Double-check after acquiring lock
            if (loadedBundles.TryGetValue(bundleName, out existing))
            {
                Interlocked.Increment(ref existing.refCount);
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
                var entry = new LoadedBundle
                {
                    bundle = bundle,
                    refCount = 1,
                    sourcePath = sourcePath,
                    bundleName = bundleName
                };
                loadedBundles[bundleName] = entry;
                return bundle;
            }

            Log.w($"Bundle not found: {bundleName}", "AssetBundleManager");
            return null;
        }
    }

    #endregion

    #region Bundle Unloading

    /// <summary>
    /// Unload a specific bundle. Decrements reference count atomically.
    /// Bundle is actually unloaded when refCount reaches 0.
    /// </summary>
    /// <param name="bundleName">Bundle name</param>
    /// <param name="unloadAllLoadedObjects">If true, also destroys all loaded assets from this bundle</param>
    public void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
    {
        if (!loadedBundles.TryGetValue(bundleName, out LoadedBundle loaded))
            return;

        int newCount = Interlocked.Decrement(ref loaded.refCount);
        if (newCount > 0) return;

        // Atomic remove only if still mapped to this exact entry
        var pair = new KeyValuePair<string, LoadedBundle>(bundleName, loaded);
        if (((ICollection<KeyValuePair<string, LoadedBundle>>)loadedBundles).Remove(pair))
        {
            try { loaded.bundle?.Unload(unloadAllLoadedObjects); } catch { }
            Log.d($"Bundle unloaded: {bundleName}", "AssetBundleManager");
        }
    }

    /// <summary>
    /// Unload all bundles. Optionally unload dependencies first.
    /// </summary>
    public void UnloadAllBundles(bool unloadAllLoadedObjects = false)
    {
        // Snapshot keys to avoid collection-modified-during-enumeration
        var keys = new List<string>(loadedBundles.Keys);
        foreach (var key in keys)
        {
            if (loadedBundles.TryRemove(key, out LoadedBundle loaded))
            {
                try { loaded.bundle?.Unload(unloadAllLoadedObjects); } catch { }
            }
        }
        Log.d("All bundles unloaded", "AssetBundleManager");
    }

    /// <summary>
    /// Unload bundles of a specific layer.
    /// </summary>
    public void UnloadBundlesByLayer(AssetBundleLayer layer, bool unloadAllLoadedObjects = false)
    {
        var m = manifest;
        if (m == null) return;

        var keys = new List<string>(loadedBundles.Keys);
        int removed = 0;
        foreach (var key in keys)
        {
            var entry = m.bundles.Find(b => b.name == key);
            if (entry != null && entry.layer == layer)
            {
                if (loadedBundles.TryRemove(key, out LoadedBundle loaded))
                {
                    try { loaded.bundle?.Unload(unloadAllLoadedObjects); } catch { }
                    removed++;
                }
            }
        }

        Log.d($"Unloaded {removed} bundles from layer: {layer}", "AssetBundleManager");
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
            Log.d($"Manifest set: v{manifest.version}, {manifest.bundles.Count} bundles", "AssetBundleManager");
        }
    }

    /// <summary>
    /// Reload manifest from hot update path after hot update completes.
    /// </summary>
    public void ReloadManifest()
    {
        try
        {
            Log.d("ReloadManifest called", "AssetBundleManager");
            Log.d($"hotUpdateBundlePath={hotUpdateBundlePath}", "AssetBundleManager");
            string manifestPath = Path.Combine(hotUpdateBundlePath, "manifest.json");
            Log.d($"Checking manifest at: {manifestPath}, exists={File.Exists(manifestPath)}", "AssetBundleManager");
            var loaded = LoadManifestFromFile(hotUpdateBundlePath);
            if (loaded != null)
            {
                manifest = loaded;
                Log.d($"Manifest reloaded: v{manifest.version}, {manifest.bundles.Count} bundles", "AssetBundleManager");
            }
            else
            {
                Log.w("Manifest reload failed, manifest is unchanged", "AssetBundleManager");
            }
        }
        catch (Exception ex)
        {
            Log.e($"ReloadManifest exception: {ex}", "AssetBundleManager");
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
        return loadedBundles.TryGetValue(bundleName, out LoadedBundle loaded)
            ? Interlocked.CompareExchange(ref loaded.refCount, 0, 0)
            : 0;
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
