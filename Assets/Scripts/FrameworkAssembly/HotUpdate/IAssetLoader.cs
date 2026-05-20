using UnityEngine;

/// <summary>
/// Interface for asset loading. Implementations can use AssetBundle, Addressables, Resources, etc.
/// This abstraction allows swapping the underlying loading mechanism without changing consumer code.
/// </summary>
public interface IAssetLoader
{
    /// <summary>
    /// Load an asset of type T from the specified bundle path and asset name.
    /// </summary>
    /// <param name="bundlePath">Relative path to the AssetBundle (e.g. "assetbundles/prefabs.ab")</param>
    /// <param name="assetName">Name of the asset within the bundle</param>
    /// <typeparam name="T">Type of UnityEngine.Object to load</typeparam>
    /// <returns>The loaded asset, or null if not found</returns>
    T LoadAsset<T>(string bundlePath, string assetName) where T : Object;

    /// <summary>
    /// Unload all cached AssetBundles and clear the cache.
    /// </summary>
    void UnloadAllBundles();
}
