using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Fetches the version manifest from the update server and compares it with the local version.
/// Supports both legacy HotUpdateManifest and enhanced AssetBundleManifest formats.
/// Supports MD5-based force re-download when version is same but code DLL changed.
/// </summary>
public class HotUpdateVersionChecker
{
    private HotUpdateConfig config;

    /// <summary>
    /// The raw AssetBundleManifest parsed from server, preserved for AssetBundleManager.
    /// </summary>
    public AssetBundleManifest RawManifest { get; private set; }

    /// <summary>
    /// Cached MD5 of the hot assembly DLL in the manifest, for force-update detection.
    /// </summary>
    public string ServerHotDllMd5 { get; private set; }

    public HotUpdateVersionChecker(HotUpdateConfig config)
    {
        this.config = config;
    }

    /// <summary>
    /// Fetch the manifest from the server as a coroutine.
    /// Tries enhanced AssetBundleManifest first, falls back to legacy HotUpdateManifest.
    /// </summary>
    public IEnumerator FetchManifestCoroutine(Action<HotUpdateManifest> onComplete)
    {
        string url = config.serverBaseUrl + "/" + config.manifestPath;
        Log.d($"Fetching manifest from: {url}", "HotUpdateVersionChecker");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = config.downloadTimeoutSeconds;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Log.w($"Failed to fetch manifest: {request.error}", "HotUpdateVersionChecker");
                onComplete?.Invoke(null);
                yield break;
            }

            string json = request.downloadHandler.text;

            // Try enhanced AssetBundleManifest first
            HotUpdateManifest manifest = ParseManifest(json);
            if (manifest != null)
            {
                CurrentManifest = manifest;
                Log.d($"Manifest fetched: version={manifest.version}, files={manifest.files.Count}", "HotUpdateVersionChecker");
                onComplete?.Invoke(manifest);
                yield break;
            }

            Log.e("Failed to parse manifest JSON in any format", "HotUpdateVersionChecker");
            onComplete?.Invoke(null);
        }
    }

    /// <summary>
    /// Parse manifest JSON. Tries enhanced format first, then legacy format.
    /// Enhanced format has "bundles" array; legacy has "files" array.
    /// </summary>
    private HotUpdateManifest ParseManifest(string json)
    {
        // Try enhanced AssetBundleManifest format
        try
        {
            AssetBundleManifest enhanced = JsonUtility.FromJson<AssetBundleManifest>(json);
            if (enhanced != null && enhanced.bundles != null && enhanced.bundles.Count > 0)
            {
                // Preserve raw manifest for AssetBundleManager
                RawManifest = enhanced;

                // Extract hot assembly DLL MD5 from manifest for force-update detection
                CacheHotDllMd5(enhanced.bundles);

                // Convert enhanced bundles to legacy file entries for backward compatibility
                var manifest = new HotUpdateManifest
                {
                    version = enhanced.version,
                    files = new List<HotUpdateFileEntry>()
                };

                foreach (var bundle in enhanced.bundles)
                {
                    manifest.files.Add(new HotUpdateFileEntry
                    {
                        name = bundle.name,
                        md5 = bundle.md5,
                        size = bundle.size
                    });
                }

                Log.d($"Parsed enhanced manifest: v{enhanced.version}, {enhanced.bundles.Count} bundles", "HotUpdateVersionChecker");
                return manifest;
            }
        }
        catch (Exception)
        {
            // Not enhanced format, try legacy
        }

        // Try legacy HotUpdateManifest format
        try
        {
            HotUpdateManifest legacy = JsonUtility.FromJson<HotUpdateManifest>(json);
            if (legacy != null && legacy.files != null && legacy.files.Count > 0)
            {
                // Extract hot assembly DLL MD5
                CacheHotDllMd5(legacy.files);

                Log.d($"Parsed legacy manifest: v{legacy.version}, {legacy.files.Count} files", "HotUpdateVersionChecker");
                return legacy;
            }
        }
        catch (Exception)
        {
            // Neither format worked
        }

        return null;
    }

    /// <summary>
    /// Extract and cache the server-side HotUpdateAssembly.dll MD5 from manifest entries.
    /// </summary>
    private void CacheHotDllMd5(List<AssetBundleEntry> bundles)
    {
        ServerHotDllMd5 = null;
        string targetName = "dll/" + HotUpdateDllLoader.HotAssemblyDllName;
        foreach (var bundle in bundles)
        {
            if (string.Equals(bundle.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                ServerHotDllMd5 = bundle.md5;
                Log.d($"Server hot DLL MD5: {ServerHotDllMd5}", "HotUpdateVersionChecker");
                return;
            }
        }
    }

    private void CacheHotDllMd5(List<HotUpdateFileEntry> files)
    {
        ServerHotDllMd5 = null;
        string targetName = "dll/" + HotUpdateDllLoader.HotAssemblyDllName;
        foreach (var file in files)
        {
            if (string.Equals(file.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                ServerHotDllMd5 = file.md5;
                Log.d($"Server hot DLL MD5 (legacy): {ServerHotDllMd5}", "HotUpdateVersionChecker");
                return;
            }
        }
    }

    /// <summary>
    /// Check if update is needed — by version AND DLL/Lua MD5.
    /// Returns true if:
    ///   1. Server version > local version (standard version update), OR
    ///   2. Version same but hot assembly DLL MD5 differs (code changed), OR
    ///   3. Version same but any .lua.enc MD5 differs (Lua changed)
    /// </summary>
    public bool IsUpdateNeeded(string serverVersion)
    {
        string localVersion = PlayerPrefs.GetString(config.versionKey, "0.0.0");
        Log.d($"Version check: local={localVersion}, server={serverVersion}", "HotUpdateVersionChecker");

        bool needed = CompareVersions(serverVersion, localVersion) > 0;
        if (needed)
        {
            Log.d("Update needed: server version is newer", "HotUpdateVersionChecker");
            return true;
        }

        // Version is same — check if any hot update file MD5 changed
        if (CompareVersions(serverVersion, localVersion) == 0)
        {
            if (IsHotDllMd5Changed()) { Log.d("Update needed: hot DLL MD5 changed", "HotUpdateVersionChecker"); return true; }
            if (IsAnyLuaMd5Changed()) { Log.d("Update needed: Lua script MD5 changed", "HotUpdateVersionChecker"); return true; }
        }

        Log.d("Update needed: False", "HotUpdateVersionChecker");
        return false;
    }

    /// <summary>
    /// Check if any .lua.enc file on disk has a different MD5 from the server manifest.
    /// </summary>
    private bool IsAnyLuaMd5Changed()
    {
        string localDir = Path.Combine(Application.persistentDataPath, config.localHotUpdateDir);
        foreach (var entry in CurrentManifest?.files ?? new List<HotUpdateFileEntry>())
        {
            if (!entry.name.EndsWith(".lua.enc", StringComparison.OrdinalIgnoreCase))
                continue;

            string localPath = Path.Combine(localDir, entry.name);
            if (!File.Exists(localPath))
                return true;  // Missing file

            string localMd5 = ComputeFileMD5(localPath);
            if (!string.Equals(localMd5, entry.md5, StringComparison.OrdinalIgnoreCase))
            {
                Log.d($"Lua MD5 mismatch for {entry.name}: local={localMd5}, server={entry.md5}", "HotUpdateVersionChecker");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Cached manifest entries for IsAnyLuaMd5Changed lookup.
    /// Set by FetchManifestCoroutine before IsUpdateNeeded is called.
    /// </summary>
    private HotUpdateManifest CurrentManifest { get; set; }

    /// <summary>
    /// Check if the hot assembly DLL on disk has a different MD5 from the server manifest.
    /// </summary>
    private bool IsHotDllMd5Changed()
    {
        if (string.IsNullOrEmpty(ServerHotDllMd5))
        {
            Log.d("No server hot DLL MD5 in manifest, skip force check", "HotUpdateVersionChecker");
            return false;
        }

        string localDllPath = Path.Combine(
            Application.persistentDataPath,
            config.localHotUpdateDir,
            "dll",
            HotUpdateDllLoader.HotAssemblyDllName
        );

        if (!File.Exists(localDllPath))
        {
            // No local copy yet, need to download
            return true;
        }

        string localMd5 = ComputeFileMD5(localDllPath);
        bool changed = !string.Equals(localMd5, ServerHotDllMd5, StringComparison.OrdinalIgnoreCase);
        if (changed)
        {
            Log.d($"Hot DLL MD5 mismatch: local={localMd5}, server={ServerHotDllMd5}", "HotUpdateVersionChecker");
        }
        return changed;
    }

    /// <summary>
    /// Get list of files that need to be downloaded (new or changed).
    /// Compares manifest files against locally cached files by MD5.
    /// </summary>
    public List<HotUpdateFileEntry> GetFilesToDownload(HotUpdateManifest manifest)
    {
        List<HotUpdateFileEntry> toDownload = new List<HotUpdateFileEntry>();
        string localDir = System.IO.Path.Combine(Application.persistentDataPath, config.localHotUpdateDir);

        foreach (var file in manifest.files)
        {
            string localPath = System.IO.Path.Combine(localDir, file.name);
            if (!System.IO.File.Exists(localPath))
            {
                toDownload.Add(file);
                continue;
            }

            // File exists, check MD5
            string localMd5 = ComputeFileMD5(localPath);
            if (!string.Equals(localMd5, file.md5, StringComparison.OrdinalIgnoreCase))
            {
                Log.d($"MD5 mismatch for {file.name}: local={localMd5}, server={file.md5}", "HotUpdateVersionChecker");
                toDownload.Add(file);
            }
        }

        Log.d($"Files to download: {toDownload.Count}/{manifest.files.Count}", "HotUpdateVersionChecker");
        return toDownload;
    }

    /// <summary>
    /// Save the current version to PlayerPrefs.
    /// </summary>
    public void SaveVersion(string version)
    {
        PlayerPrefs.SetString(config.versionKey, version);
        PlayerPrefs.Save();
        Log.d($"Version saved: {version}", "HotUpdateVersionChecker");
    }

    /// <summary>
    /// Compute MD5 hash of a file.
    /// </summary>
    public static string ComputeFileMD5(string filePath)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        using (var stream = System.IO.File.OpenRead(filePath))
        {
            byte[] hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Compare two semantic version strings. Returns >0 if a > b, 0 if equal, <0 if a < b.
    /// </summary>
    private int CompareVersions(string a, string b)
    {
        try
        {
            Version va = new Version(a);
            Version vb = new Version(b);
            return va.CompareTo(vb);
        }
        catch
        {
            // Fallback to string comparison if parsing fails
            return string.Compare(a, b, StringComparison.Ordinal);
        }
    }
}
