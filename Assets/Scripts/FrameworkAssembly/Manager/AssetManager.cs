using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AssetManager
{
    #region Singleton
    private static AssetManager instance;
    public static AssetManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new AssetManager();
            }
            return instance;
        }
    }
    private AssetManager()
    {
        loadRootPath = Application.dataPath;
    }
    #endregion

    private string loadRootPath;
    private IAssetLoader assetLoader;

    /// <summary>
    /// Set the asset loader implementation. Called by HotUpdateManager after initialization.
    /// </summary>
    public void SetAssetLoader(IAssetLoader loader)
    {
        assetLoader = loader;
        Log.d($"AssetLoader set: {loader?.GetType().Name}", "AssetManager");
    }

    public T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = null;
        try
        {
#if UNITY_EDITOR
            // In Editor, prefer AssetDatabase for fast iteration
            asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }
#endif
            // Runtime: use IAssetLoader if available
            if (assetLoader != null)
            {
                // Derive bundle path and asset name from the full path
                // e.g. "Assets/Resources/Prefabs/UILogin.prefab" → bundle="prefabs.ab", asset="UILogin"
                string bundlePath = DeriveBundlePath(path);
                string assetName = DeriveAssetName(path);
                asset = assetLoader.LoadAsset<T>(bundlePath, assetName);
                if (asset != null)
                {
                    return asset;
                }
            }

            // Fallback: try Resources.Load
            string resourcesPath = PathToResourcesPath(path);
            if (!string.IsNullOrEmpty(resourcesPath))
            {
                asset = Resources.Load<T>(resourcesPath);
            }

            if (asset == null)
            {
                Log.w($"LoadAsset: asset not found for path: {path}", "AssetManager");
            }
        }
        catch (Exception e)
        {
            Log.e(e, "AssetManager");
        }
        return asset;
    }

    /// <summary>
    /// Derive AssetBundle path from the full asset path.
    /// Maps directory structure to bundle names matching AssetBundleBuildRules.
    /// e.g. "Assets/ProjectAssets/Base/UIAssets/Prefabs/..." → "base_base_prefab.ab"
    /// e.g. "Assets/ProjectAssets/HotUpdate/UIAssets/Prefabs/..." → "hotfix_hotupdate_prefab.ab"
    /// </summary>
    private string DeriveBundlePath(string fullPath)
    {
        string dir = System.IO.Path.GetDirectoryName(fullPath);
        if (dir == null) return "assetbundles/root.ab";

        string normalizedDir = dir.Replace('\\', '/');

        // Map directory to bundle name based on three-tier rules
        // The bundle name format is: {layer}_{module}_{resType}.ab
        // We pass the full path to HotUpdateAssetLoader which uses MapPathToBundleName
        // to find the actual bundle in the manifest.
        // Here we just need a unique key that MapPathToBundleName can work with.
        return normalizedDir;
    }

    /// <summary>
    /// Derive asset name from the full asset path.
    /// e.g. "Assets/Resources/Prefabs/UILogin.prefab" → "UILogin"
    /// </summary>
    private string DeriveAssetName(string fullPath)
    {
        return System.IO.Path.GetFileNameWithoutExtension(fullPath);
    }

    /// <summary>
    /// Convert an asset path to a Resources-relative path.
    /// e.g. "Assets/Resources/Prefabs/UILogin.prefab" → "Prefabs/UILogin"
    /// </summary>
    private string PathToResourcesPath(string fullPath)
    {
        if (fullPath.Contains("Resources/"))
        {
            int idx = fullPath.IndexOf("Resources/", StringComparison.Ordinal);
            string relative = fullPath.Substring(idx + "Resources/".Length);
            return System.IO.Path.ChangeExtension(relative, null);
        }
        return null;
    }
}
