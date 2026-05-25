using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PureMVC.Patterns.Facade;
using PureMVC.Interfaces;
using UnityEngine;

/// <summary>
/// Entry point MonoBehaviour. Lives in AOTAssembly so IL2CPP does not strip it.
///
/// Assembly Architecture (refactored):
///   FrameworkAssembly (AOT, direct reference) — PureMVC Core, Managers, Commands, Const, CustomBase,
///     Network, HotUpdate system, Common, UIComponent, all UI Mediators, all Proxies
///   HotUpdateAssembly (hot-updatable, via _hotAssembly) — AchievementProxy, PlayerExtProxy,
///     UICreatePlayerMediator, UIMainMediator, HotUpdateStartupMacroCommand, LoginSuccessCommand,
///     HotUpdate Const classes, HotNetworkMessageHelper, GameConfigCs
///
/// Assembly Loading Strategy:
///   FrameworkAssembly: directly referenced (AOT), all managers/commands/const accessed via direct types.
///   HotUpdateAssembly:
///     Editor: Type.GetType from a type still in HotUpdateAssembly
///     Runtime: persistentDataPath/cache/HotUpdateAssembly.dll > Resources fallback
///
/// Hot Update Strategy:
///   Download new HotUpdateAssembly.dll → write to persistent cache → restart app → new code active.
///   IL2CPP does not support runtime assembly reload.
/// </summary>
public class GameMain : MonoBehaviour
{
    // AOT generic pre-instantiation — prevents "AOT generic method not instantiated" errors
    // These are used by NetworkManager in FrameworkAssembly
    private static readonly ConcurrentQueue<(int, byte[])> _aot_queue = new ConcurrentQueue<(int, byte[])>();
    private static readonly Queue<(int, byte[])> _aot_pending = new Queue<(int, byte[])>();

    public static GameMain Instance { get; private set; }

    /// <summary>
    /// The currently active HotUpdateAssembly. Loaded once at startup from
    /// persistentCache > Resources, then optionally replaced by ApplyHotUpdate.
    /// Only used for types that remain in HotUpdateAssembly.
    /// </summary>
    private Assembly _hotAssembly;

    // HotUpdateAssembly command type (still uses reflection)
    private Type _cmdLoginSuccess;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadBaseHotUpdateAssembly();
    }

    /// <summary>
    /// Resolve the HotUpdateAssembly reference.
    /// 
    /// Editor: Unity auto-compiles and loads the assembly. Use a type still in HotUpdateAssembly.
    /// Runtime (IL2CPP): HotUpdateAssembly is AOT-embedded in the APK. If a hot-updated
    ///   DLL exists in persistentDataPath/cache, load that instead (takes effect next
    ///   cold start after ApplyHotUpdate writes it).
    /// </summary>
    void LoadBaseHotUpdateAssembly()
    {
#if UNITY_EDITOR
        // Editor: resolve any type from HotUpdateAssembly to get the Assembly.
        var editorType = Type.GetType("HotUpdateStartupMacroCommand,HotUpdateAssembly");
        if (editorType != null)
        {
            _hotAssembly = editorType.Assembly;
            Log.d("HotUpdateAssembly resolved in Editor", "GameMain");
            return;
        }
        Log.e("Cannot resolve HotUpdateAssembly in Editor — check compilation errors", "GameMain");
        return;
#else
        // Runtime: priority chain — persistent cache > AOT-embedded
        string cacheDir = Path.Combine(Application.persistentDataPath, "HotUpdate", "cache");
        string cachePath = Path.Combine(cacheDir, "HotUpdateAssembly.dll");

        if (File.Exists(cachePath))
        {
            try
            {
                _hotAssembly = Assembly.Load(File.ReadAllBytes(cachePath));
                Log.d("HotUpdateAssembly loaded from persistent cache", "GameMain");
                return;
            }
            catch (Exception e)
            {
                Log.w($"Failed to load cached HotUpdateAssembly: {e.Message}", "GameMain");
            }
        }

        // Fallback: load from Resources/HotUpdateAssembly.bytes
        TextAsset dllAsset = Resources.Load<TextAsset>("HotUpdateAssembly");
        if (dllAsset != null)
        {
            try
            {
                _hotAssembly = Assembly.Load(dllAsset.bytes);
                Log.d("HotUpdateAssembly loaded from Resources", "GameMain");
                return;
            }
            catch (Exception e)
            {
                Log.e($"Failed to load HotUpdateAssembly from Resources: {e.Message}", "GameMain");
            }
        }

        Log.e("Cannot resolve HotUpdateAssembly — app may not function", "GameMain");
#endif
    }

    void Start()
    {
        // Init managers first — DialogManager available for error reporting
        InitManagers();

        if (_hotAssembly == null)
        {
            Log.e("HotUpdateAssembly not loaded, cannot start", "GameMain");
            DialogManager.Instance.ShowInfo("Fatal Error",
                "Hot update assembly failed to load.\nPlease restart the application.",
                () => Application.Quit());
            return;
        }

        // Resolve LoginSuccessCommand type from HotUpdateAssembly (the only command in hot-update assembly)
        _cmdLoginSuccess = _hotAssembly?.GetType("LoginSuccessCommand");

        StartCoroutine(StartupFlow());
    }

    /// <summary>
    /// Called by HotUpdateManager after a successful DLL download.
    /// Writes the new DLL to persistent cache for next cold start.
    /// </summary>
    public void ApplyHotUpdate(byte[] dllBytes)
    {
        if (dllBytes == null || dllBytes.Length == 0)
        {
            Log.e("ApplyHotUpdate: null or empty DLL bytes", "GameMain");
            return;
        }

        string cacheDir = Path.Combine(Application.persistentDataPath, "HotUpdate", "cache");
        string cachePath = Path.Combine(cacheDir, "HotUpdateAssembly.dll");
        try
        {
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            File.WriteAllBytes(cachePath, dllBytes);
            Log.d("Hot assembly cache updated for next startup", "GameMain");
        }
        catch (Exception e)
        {
            Log.e($"ApplyHotUpdate: failed to write cache: {e.Message}", "GameMain");
        }
    }

    /// <summary>
    /// Invoke a static no-arg RegisterTo() method on a HotUpdateAssembly const class.
    /// </summary>
    void InvokeHotUpdateConst(string typeName)
    {
        var type = _hotAssembly?.GetType(typeName);
        if (type != null)
        {
            var m = type.GetMethod("RegisterTo", BindingFlags.Public | BindingFlags.Static);
            m?.Invoke(null, null);
            Log.d($"{typeName} registered", "GameMain");
        }
    }

    void InitManagers()
    {
        // Trigger singleton initialization for all managers (FrameworkAssembly — direct access)
        UIManager.Instance.Init();
        HotUpdateManager.Instance.Initialize();
        AssetManager.Instance.SetAssetLoader(null); // will be set after hot update completes
        // Skip AB init in Editor — AssetDatabase provides assets directly
#if !UNITY_EDITOR
        AssetBundleManager.Instance.Initialize("HotUpdate");
#endif
        NetworkManager.Instance.GetHashCode(); // ensure awake triggered
        UpdateManager.Instance.GetHashCode();
        TimerManager.Instance.GetHashCode();
        AudioManager.Instance.GetHashCode();
        ObjectPoolManager.Instance.GetHashCode();
        GameSceneManager.Instance.GetHashCode();

        // Managers accessed via Instance for actual API calls
        RedDotManager.Instance.Initialize();
        DialogManager.Instance.Initialize();

        Log.d("All managers initialized", "GameMain");
    }

    IEnumerator StartupFlow()
    {
        Log.d("Starting hot update check...", "GameMain");

        // Init, GameStart sends STARTUP → HotUpdateCommand → check → UI or success
        InitModule();
        GameStart();  // This sends STARTUP → StartupMacroCommand → HotUpdateCommand
        ConnectServer();

        // Editor: skip wait (AssetDatabase provides assets directly)
#if !UNITY_EDITOR
        float timeout = 30f;
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            int stateVal = (int)HotUpdateManager.Instance.State;
            if (stateVal == 4 || stateVal == 5 || elapsed > timeout) break;
            yield return null;
        }

        // Reload AB manifest so newly downloaded bundles are available
        AssetBundleManager.Instance.ReloadManifest();

        // AssetManager.SetAssetLoader(HotUpdateManager.Instance.AssetLoader)
        AssetManager.Instance.SetAssetLoader(HotUpdateManager.Instance.AssetLoader);

        // Reload red dot tree if hot update delivered a new RedDotTree.json
        RedDotManager.Instance.ReloadTree();
#endif

        // Open login only if no restart is needed
        if (!HotUpdateManager.Instance.NeedRestart)
        {
            OpenLogin();
        }
        yield break;
    }

    void InitModule()
    {
        // Init Framework UIConst (instance singleton)
        UIConst.Instance.Init();

        // Register hot-update UI definitions via reflection
        var hotUpdateUIConstType = _hotAssembly?.GetType("HotUpdateUIConst");
        if (hotUpdateUIConstType != null)
        {
            var registerToMethod = hotUpdateUIConstType.GetMethod("RegisterTo", BindingFlags.Public | BindingFlags.Static);
            registerToMethod?.Invoke(null, new object[] { UIConst.Instance });
            Log.d("HotUpdateUIConst registered", "GameMain");
        }

        // Register hot-update consts via reflection (all no-arg RegisterTo methods)
        InvokeHotUpdateConst("HotUpdateNotificationConst");
        InvokeHotUpdateConst("HotUpdateMessageConst");
        InvokeHotUpdateConst("HotUpdateProxyConst");
        InvokeHotUpdateConst("HotUpdatePlayerPrefsConst");

        // Initialize Lua subsystem
        LuaBootstrap.Instance.Initialize();
    }

    void GameStart()
    {
        var facade = Facade.Instance;

        // Register all commands in FrameworkAssembly (direct types)
        facade.RegisterCommand(NotificationConst.STARTUP,             () => new StartupMacroCommand());
        facade.RegisterCommand(NotificationConst.HOT_UPDATE_CHECK,   () => new HotUpdateCommand());
        facade.RegisterCommand(NotificationConst.LOGIN,              () => new LoginCommand());
        facade.RegisterCommand(NotificationConst.REGISTER,           () => new RegisterCommand());
        facade.RegisterCommand(NotificationConst.CREATE_PLAYER,      () => new CreatePlayerCommand());
        facade.RegisterCommand(NetworkNotificationConst.NETWORK_DISCONNECTED, () => new NetworkDisconnectedCommand());
        facade.RegisterCommand(NetworkNotificationConst.NETWORK_CONNECTED,    () => new NetworkConnectedCommand());

        // Register LoginSuccessCommand from HotUpdateAssembly (via reflection)
        if (_cmdLoginSuccess != null)
        {
            facade.RegisterCommand(NotificationConst.LOGIN_SUCCESS, () =>
                (ICommand)Activator.CreateInstance(_cmdLoginSuccess));
        }

        // Execute hot-update startup macro command (registers hot-update proxies + commands)
        var hotStartupCmdType = _hotAssembly?.GetType("HotUpdateStartupMacroCommand");
        if (hotStartupCmdType != null)
        {
            var hotStartupCmd = Activator.CreateInstance(hotStartupCmdType) as IHotUpdateStartup;
            if (hotStartupCmd != null)
            {
                var notif = new PureMVC.Patterns.Observer.Notification("HOT_UPDATE_STARTUP", null, null);
                hotStartupCmd.Execute(notif);
                Log.d("HotUpdateStartupMacroCommand executed", "GameMain");
            }
        }

        Facade.Instance.SendNotification(NotificationConst.STARTUP);
    }

    void ConnectServer()
    {
        NetworkManager.Instance.Connect();
    }

    void OpenLogin()
    {
        UIManager.Instance.OpenUI<UILoginMediator>(UIConst.UILogin);
    }
}
