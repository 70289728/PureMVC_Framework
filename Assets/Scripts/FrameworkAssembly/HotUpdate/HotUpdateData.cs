using System;
using System.Collections.Generic;

/// <summary>
/// Single file entry in the hot update version manifest.
/// </summary>
[Serializable]
public class HotUpdateFileEntry
{
    public string name;
    public string md5;
    public long size;
}

/// <summary>
/// Version manifest downloaded from the update server.
/// </summary>
[Serializable]
public class HotUpdateManifest
{
    public string version;
    public List<HotUpdateFileEntry> files;
}

/// <summary>
/// State machine for the hot update lifecycle.
/// </summary>
public enum HotUpdateState
{
    Idle,
    Checking,
    UpdateAvailable,   // Update is available, waiting for user confirmation (HOT_UPDATE_AVAILABLE fired)
    Downloading,
    Verifying,
    Applying,
    Success,
    Failed
}

#region Enhanced AssetBundle Manifest (v2)

/// <summary>
/// Resource type classification for AssetBundle grouping.
/// </summary>
public enum AssetBundleResourceType
{
    Unknown = 0,
    Prefab = 1,
    Texture = 2,
    Config = 3,
    Audio = 4,
    Shader = 5,
    Font = 6,
    Lua = 7,
    Animation = 8,
    Material = 9,
    Video = 10,
}

/// <summary>
/// AssetBundle layer in the three-tier packaging model.
/// </summary>
public enum AssetBundleLayer
{
    Base = 0,       // Shipped with app, always loaded
    Module = 1,     // Loaded on demand per feature
    Hotfix = 2,     // Downloaded at runtime, overrides Base/Module
}

/// <summary>
/// Compression format for AssetBundles.
/// </summary>
public enum BundleCompression
{
    LZ4 = 0,        // Recommended: fast decompression, chunk-based
    LZMA = 1,       // Smaller size, must decompress entirely before use
    Uncompressed = 2,
}

/// <summary>
/// Enhanced single bundle entry with full metadata.
/// </summary>
[Serializable]
public class AssetBundleEntry
{
    public string name;                         // Bundle file name: "module_login_prefab.ab"
    public string md5;                          // MD5 hash (fast, for quick diff)
    public string sha256;                       // SHA256 hash (secure, for integrity)
    public string crc32;                        // CRC32 checksum (fast corruption check)
    public long size;                           // File size in bytes
    public AssetBundleLayer layer;              // Base / Module / Hotfix
    public BundleCompression compression;       // LZ4 / LZMA / Uncompressed
    public AssetBundleResourceType resourceType;// Prefab / Texture / Config / ...
    public List<string> dependencies;           // Names of bundles this depends on
    public List<string> assets;                 // Asset paths contained in this bundle
}

/// <summary>
/// Enhanced version manifest with dependency graph and full bundle metadata.
/// </summary>
[Serializable]
public class AssetBundleManifest
{
    public string version;                      // Semantic version: "1.0.1"
    public string buildTime;                    // ISO 8601 build timestamp
    public string platform;                     // "Windows" / "Android" / "iOS"
    public List<AssetBundleEntry> bundles;      // All bundle entries
    public Dictionary<string, List<string>> dependencyGraph; // bundleName → [depNames]
}

#endregion
