using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Loads assets from hot-updated AssetBundles with fallback to built-in Resources.
/// Implements IAssetLoader for future Addressables migration.
/// Uses AssetBundleManager for three-tier loading and dependency resolution.
/// </summary>
public class HotUpdateAssetLoader : IAssetLoader
{
    private HotUpdateConfig config;

    public HotUpdateAssetLoader(HotUpdateConfig config)
    {
        this.config = config;
    }

    /// <summary>
    /// Load an asset from a hot-updated AssetBundle, falling back to Resources.
    /// </summary>
    public T LoadAsset<T>(string bundlePath, string assetName) where T : UnityEngine.Object
    {
        // Try AssetBundleManager first (handles three-tier: Hotfix > Module > Base)
        T asset = LoadFromAssetBundleManager<T>(bundlePath, assetName);
        if (asset != null)
        {
            return asset;
        }

        // Fallback to Resources
        asset = LoadFromResources<T>(bundlePath, assetName);
        if (asset != null)
        {
            return asset;
        }

        Log.w($"Asset not found: {assetName} in bundle {bundlePath}", "HotUpdateAssetLoader");
        return null;
    }

    private T LoadFromAssetBundleManager<T>(string bundlePath, string assetName) where T : UnityEngine.Object
    {
        if (AssetBundleManager.Instance == null)
        {
            return null;
        }

        // Convert path like "assetbundles/prefabs.ab" to bundle name "module_login_prefab.ab"
        // The bundlePath from AssetManager is derived from directory structure.
        // We need to map it to the actual bundle name from the three-tier rules.
        string bundleName = MapPathToBundleName(bundlePath);

        T asset = AssetBundleManager.Instance.LoadAsset<T>(bundleName, assetName);
        if (asset != null)
        {
            Log.d($"Loaded via AssetBundleManager: {assetName} from {bundleName}", "HotUpdateAssetLoader");
        }
        return asset;
    }

    /// <summary>
    /// Map a directory path to the actual three-tier bundle name.
    /// e.g. "Assets/ProjectAssets/Base/UIAssets/Prefabs/..." → "base_base_prefab.ab"
    /// e.g. "Assets/ProjectAssets/HotUpdate/UIAssets/Prefabs/..." → "hotfix_hotupdate_prefab.ab"
    /// Matches by checking which bundle's assets list contains files under this directory.
    /// </summary>
    private string MapPathToBundleName(string dirPath)
    {
        string normalizedPath = dirPath.Replace('\\', '/').TrimEnd('/');

        var manifest = AssetBundleManager.Instance.GetManifest();
        if (manifest != null)
        {
            // Find bundle whose assets are under this directory
            foreach (var entry in manifest.bundles)
            {
                if (entry.assets != null)
                {
                    foreach (string asset in entry.assets)
                    {
                        string assetPath = asset.Replace('\\', '/');
                        if (assetPath.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase))
                        {
                            return entry.name;
                        }
                    }
                }
            }
        }

        // Fallback: use the path as-is
        return dirPath;
    }

    private T LoadFromResources<T>(string bundlePath, string assetName) where T : UnityEngine.Object
    {
        T asset = Resources.Load<T>(assetName);
        if (asset != null)
        {
            Log.d($"Loaded from Resources: {assetName} ({typeof(T).Name})", "HotUpdateAssetLoader");
        }
        return asset;
    }

    /// <summary>
    /// Unload all cached AssetBundles via AssetBundleManager.
    /// </summary>
    public void UnloadAllBundles()
    {
        AssetBundleManager.Instance?.UnloadAllBundles(false);
        Log.d("All AssetBundles unloaded", "HotUpdateAssetLoader");
    }
}
