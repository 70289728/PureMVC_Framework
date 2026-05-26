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

    // Debounce flags for StartCheck / StartDownload to prevent duplicate coroutine launches
    private bool _isChecking = false;
    private bool _isDownloading = false;

    // Cached reflection: GameMain.ApplyHotUpdate(byte[]) — resolved once on first DLL apply
    private static System.Reflection.MethodInfo _applyHotUpdateMethod;
    private static object _gameMainInstance;
    private static bool _applyReflectionResolved = false;

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
        if (_isChecking)
        {
            Log.w("StartCheck already running, ignoring duplicate call", "HotUpdateManager");
            return;
        }
        _isChecking = true;
        CoroutineRunner.Instance.StartCoroutine(CheckCoroutineWrapper());
    }

    private IEnumerator CheckCoroutineWrapper()
    {
        yield return CheckCoroutine();
        _isChecking = false;
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
            SetState(HotUpdateState.Success, "All files up to date");
            yield break;
        }

        // Show update info and wait for user confirmation.
        // Open UI first (registers mediator), then set state so the mediator
        // receives HOT_UPDATE_STATE_CHANGED with UpdateAvailable and displays update info.
        Facade.Instance.SendNotification(NotificationConst.HOT_UPDATE_AVAILABLE);
        SetState(HotUpdateState.UpdateAvailable, $"Update available: {filesToDownload.Count} files");
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
        if (_isDownloading)
        {
            Log.w("StartDownload already running, ignoring duplicate call", "HotUpdateManager");
            return;
        }
        _isDownloading = true;
        CoroutineRunner.Instance.StartCoroutine(DownloadCoroutineWrapper());
    }

    private IEnumerator DownloadCoroutineWrapper()
    {
        yield return DownloadCoroutine();
        _isDownloading = false;
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
            // Resolve GameMain.ApplyHotUpdate(byte[]) once, then cache MethodInfo+instance.
            ResolveApplyHotUpdateReflection();
            if (_applyHotUpdateMethod != null && _gameMainInstance != null)
            {
                try
                {
                    _applyHotUpdateMethod.Invoke(_gameMainInstance, new object[] { hotDllBytes });
                    dllApplied = true;
                    Log.d("Hot update DLL cached for next startup", "HotUpdateManager");
                }
                catch (Exception e)
                {
                    Log.e($"ApplyHotUpdate invocation failed: {e.Message}", "HotUpdateManager");
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

    /// <summary>
    /// Resolve GameMain.ApplyHotUpdate(byte[]) reflection once and cache it.
    /// GameMain lives in AOTAssembly which FrameworkAssembly cannot reference, so reflection is required.
    /// </summary>
    private void ResolveApplyHotUpdateReflection()
    {
        if (_applyReflectionResolved) return;
        _applyReflectionResolved = true;

        var gameMainType = Type.GetType("GameMain,AOTAssembly") ?? Type.GetType("GameMain");
        if (gameMainType == null)
        {
            Log.w("GameMain type not found via reflection", "HotUpdateManager");
            return;
        }

        var instanceProp = gameMainType.GetProperty("Instance",
            BindingFlags.Public | BindingFlags.Static);
        _gameMainInstance = instanceProp?.GetValue(null);
        if (_gameMainInstance == null)
        {
            Log.w("GameMain.Instance is null", "HotUpdateManager");
            return;
        }

        _applyHotUpdateMethod = gameMainType.GetMethod("ApplyHotUpdate",
            BindingFlags.Public | BindingFlags.Instance);
        if (_applyHotUpdateMethod == null)
        {
            Log.w("GameMain.ApplyHotUpdate method not found", "HotUpdateManager");
        }
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
