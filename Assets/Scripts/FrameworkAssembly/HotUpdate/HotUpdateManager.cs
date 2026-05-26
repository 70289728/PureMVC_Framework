using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PureMVC.Patterns.Facade;
using UnityEngine;

/// <summary>
/// Orchestrates the entire hot update lifecycle.
/// Singleton that coordinates version check, download, verification, and loading.
/// </summary>
public class HotUpdateManager
{
    #region Singleton
    private static HotUpdateManager instance;
    public static HotUpdateManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new HotUpdateManager();
            }
            return instance;
        }
    }
    private HotUpdateManager() { }
    #endregion

    public HotUpdateConfig Config { get; private set; }
    public HotUpdateState State { get; private set; } = HotUpdateState.Idle;
    public HotUpdateManifest CurrentManifest { get; private set; }
    public float Progress { get; private set; } // 0.0 - 1.0
    public string StatusMessage { get; private set; } = "";
    public bool NeedRestart { get; private set; } // true after a successful download — client should skip login

    private HotUpdateVersionChecker versionChecker;
    private HotUpdateDownloader downloader;
    private HotUpdateDllLoader dllLoader;
    private HotUpdateAssetLoader assetLoader;
    private HotUpdateLuaLoader luaLoader;

    private bool isInitialized = false;

    public IAssetLoader AssetLoader => assetLoader;
    public ILuaLoader LuaLoader => luaLoader;

    /// <summary>
    /// Initialize the hot update system. Must be called before StartCheckAsync.
    /// Safe to call multiple times — subsequent calls are ignored.
    /// </summary>
    public void Initialize()
    {
        if (isInitialized)
        {
            Log.d("HotUpdateManager already initialized, skipping", "HotUpdateManager");
            return;
        }

        Config = Resources.Load<HotUpdateConfig>("HotUpdateConfig");

        if (Config == null)
        {
            Log.w("HotUpdateConfig not found in Resources, using default settings", "HotUpdateManager");
            Config = ScriptableObject.CreateInstance<HotUpdateConfig>();
        }

        versionChecker = new HotUpdateVersionChecker(Config);
        downloader = new HotUpdateDownloader(Config);
        dllLoader = new HotUpdateDllLoader(Config);
        assetLoader = new HotUpdateAssetLoader(Config);
        AssetManager.Instance.SetAssetLoader(assetLoader);
        luaLoader = new HotUpdateLuaLoader(Config);

        downloader.OnProgress += OnDownloadProgress;
        downloader.OnFileComplete += OnFileDownloadComplete;

        isInitialized = true;
        Log.d("HotUpdateManager initialized", "HotUpdateManager");
    }

    /// <summary>
    /// Start the hot update CHECK only. Fetches manifest and compares version.
    /// Fires HOT_UPDATE_STATE_CHANGED with Idle when update is needed — UI should then
    /// show the update info and let user decide (confirm/cancel).
    /// On confirm, call StartDownload().
    /// </summary>
    public void StartCheck()
    {
        if (!isInitialized)
        {
            Log.e("HotUpdateManager not initialized", "HotUpdateManager");
            return;
        }
        CoroutineRunner.Instance.StartCoroutine(CheckCoroutine());
    }

    private IEnumerator CheckCoroutine()
    {
        SetState(HotUpdateState.Checking, "Checking for updates...");

        // Step 1: Fetch manifest
        HotUpdateManifest manifest = null;
        yield return versionChecker.FetchManifestCoroutine((m) => { manifest = m; });

        if (manifest == null)
        {
            Log.w("Failed to fetch manifest, using built-in resources", "HotUpdateManager");
            // Editor / no-server: don't signal Success — just stay idle
            SetState(HotUpdateState.Idle, "No server — using built-in");
            yield break;
        }

        CurrentManifest = manifest;

        // Step 2: Check if update is needed
        if (!versionChecker.IsUpdateNeeded(manifest.version))
        {
            Log.d("No update needed, version is up to date", "HotUpdateManager");
            SetState(HotUpdateState.Success, "Version is up to date");
            yield break;
        }

        // Step 3: Get files to download (for info display)
        filesToDownload = versionChecker.GetFilesToDownload(manifest);
        if (filesToDownload.Count == 0)
        {
            Log.d("All files are up to date", "HotUpdateManager");
            versionChecker.SaveVersion(manifest.version);
            // Save manifest so AssetBundleManager can discover existing bundles after restart
            SaveManifestToDisk();
            // Also update the in-memory manifest so ReloadManifest picks up hotfix bundles
            AssetBundleManager.Instance.SetManifest(versionChecker.RawManifest);
            AssetBundleManager.Instance.ReloadManifest();
            SetState(HotUpdateState.Success, "All files up to date");
            yield break;
        }

        // Show update info and wait for user confirmation.
        // Open UI first (registers mediator), then set state so the mediator
        // receives HOT_UPDATE_STATE_CHANGED with Idle and displays update info.
        Facade.Instance.SendNotification(NotificationConst.HOT_UPDATE_AVAILABLE);
        SetState(HotUpdateState.Idle, $"Update available: {filesToDownload.Count} files");
    }

    private List<HotUpdateFileEntry> filesToDownload;

    /// <summary>
    /// Start downloading files. Called by UI after user confirms.
    /// </summary>
    public void StartDownload()
    {
        if (filesToDownload == null || filesToDownload.Count == 0)
        {
            Log.w("No files to download", "HotUpdateManager");
            return;
        }
        CoroutineRunner.Instance.StartCoroutine(DownloadCoroutine());
    }

    private IEnumerator DownloadCoroutine()
    {
        // Step 4: Download files
        SetState(HotUpdateState.Downloading, $"Downloading {filesToDownload.Count} files...");
        bool downloadSuccess = false;
        bool hasLuaUpdate = HasLuaFiles(filesToDownload);
        yield return downloader.DownloadFilesCoroutine(filesToDownload, (ok) => downloadSuccess = ok);

        if (!downloadSuccess)
        {
            Log.e("Download failed, using built-in resources", "HotUpdateManager");
            SetState(HotUpdateState.Failed, "Download failed");
            yield break;
        }

        // Step 5: Verify
        SetState(HotUpdateState.Verifying, "Verifying files...");
        versionChecker.SaveVersion(CurrentManifest.version);

        // Step 6: Apply DLLs
        SetState(HotUpdateState.Applying, "Applying updates...");
        dllLoader.LoadAOTMetadata();

        bool dllApplied = false;
        if (dllLoader.ReadHotUpdateAssemblyBytes(out byte[] hotDllBytes))
        {
            var gameMainType = System.Type.GetType("GameMain,AOTAssembly")
                           ?? System.Type.GetType("GameMain");
            if (gameMainType != null)
            {
                var instanceProp = gameMainType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var gm = instanceProp?.GetValue(null);
                if (gm != null)
                {
                    var applyMethod = gameMainType.GetMethod("ApplyHotUpdate",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (applyMethod != null)
                    {
                        applyMethod.Invoke(gm, new object[] { hotDllBytes });
                        dllApplied = true;
                        Log.d("Hot update DLL cached for next startup", "HotUpdateManager");
                    }
                }
            }
            if (!dllApplied) dllApplied = dllLoader.LoadHotUpdateAssembly();
        }

        // Step 7: Success
        // Set up AssetLoader and manifest BEFORE sending the restart notification,
        // otherwise RestartCommand.OpenUI(UIReStart) fails because AssetManager
        // has no loader yet and falls back to Resources.Load.
        AssetBundleManager.Instance.SetManifest(versionChecker.RawManifest);
        AssetManager.Instance.SetAssetLoader(assetLoader);

        SetState(HotUpdateState.Success, "Update complete");
        NeedRestart = true;

        // Notify that a restart is needed to apply the new DLL
        Facade.Instance.SendNotification(NotificationConst.HOT_UPDATE_NEED_RESTART);

        // Reload Lua
        luaLoader?.ClearCache();
        if (hasLuaUpdate && luaLoader != null)
        {
            try
            {
                var lb = LuaBootstrap.Instance;
                if (lb != null && lb.IsInitialized)
                {
                    lb.ReloadAfterHotUpdate();
                    lb.PromptRestart();
                }
            }
            catch (Exception e)
            {
                Log.w($"Failed to reload Lua: {e.Message}", "HotUpdateManager");
            }
        }

        // Save manifest to persistentDataPath so restart picks up new bundles
        SaveManifestToDisk();
    }

    private void SaveManifestToDisk()
    {
        string dir = Path.Combine(Application.persistentDataPath, Config.localHotUpdateDir);
        string path = Path.Combine(dir, "manifest.json");
        try
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string json = JsonUtility.ToJson(versionChecker.RawManifest, true);
            File.WriteAllText(path, json);
            Log.d($"Manifest saved to {path}", "HotUpdateManager");
        }
        catch (Exception e)
        {
            Log.e($"Failed to save manifest: {e.Message}", "HotUpdateManager");
        }
    }

    private void SetState(HotUpdateState newState, string message)
    {
        State = newState;
        StatusMessage = message;
        Log.d($"State: {newState} - {message}", "HotUpdateManager");

        // Send PureMVC notification
        Facade.Instance.SendNotification(NotificationConst.HOT_UPDATE_STATE_CHANGED, newState);
    }

    private void OnDownloadProgress(int currentFile, int totalFiles, long downloadedBytes, long totalBytes)
    {
        if (totalBytes > 0)
        {
            Progress = (float)downloadedBytes / totalBytes;
        }

        var progressData = new HotUpdateProgressData
        {
            currentFile = currentFile,
            totalFiles = totalFiles,
            downloadedBytes = downloadedBytes,
            totalBytes = totalBytes,
            progress = Progress
        };

        Facade.Instance.SendNotification(NotificationConst.HOT_UPDATE_PROGRESS, progressData);
    }

    private void OnFileDownloadComplete(string fileName)
    {
        Log.d($"File complete: {fileName}", "HotUpdateManager");
    }

    private static bool HasLuaFiles(List<HotUpdateFileEntry> files)
    {
        foreach (var f in files)
        {
            if (f.name.Contains(".lua.enc") || f.name.Contains(".lua.txt"))
                return true;
        }
        return false;
    }
}

/// <summary>
/// Progress data sent with HOT_UPDATE_PROGRESS notification.
/// </summary>
public class HotUpdateProgressData
{
    public int currentFile;
    public int totalFiles;
    public long downloadedBytes;
    public long totalBytes;
    public float progress; // 0.0 - 1.0
}
