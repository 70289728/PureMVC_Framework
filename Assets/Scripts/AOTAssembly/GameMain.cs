using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Entry point MonoBehaviour. Lives in AOTAssembly so IL2CPP does not strip it.
///
/// Assembly Architecture (refactored):
///   FrameworkAssembly (AOT) — PureMVC Core, Managers, Commands, Const, CustomBase,
///     Network, HotUpdate system, Common, UIComponent, UILoginMediator, UIHotUpdateMediator,
///     UserProxy, NetworkProxy, HotUpdateProxy
///   HotUpdateAssembly (hot-updatable) — BagProxy, ShopProxy, UIBagMediator,
///     UICreatePlayerMediator, UIMainMediator, UIShopMediator, UIShopGoodItemMediator,
///     UIShopTabItemMediator, GameConfigCs, ProtoScripts
///
/// Assembly Loading Strategy:
///   FrameworkAssembly: directly referenced (AOT), types resolved via Type.GetType("xxx,FrameworkAssembly")
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

    // AOT generic pre-instantiation for UIManager.OpenUI<T> — not needed here.
    // HotUpdateAssembly already references FrameworkAssembly directly and generates
    // its own OpenUI<T> generic instances (via LoginSuccessCommand / UIMainMediator etc.).
    // Adding AOTAssembly → HotUpdateAssembly reference would create an unwanted compile-time
    // coupling between the AOT entry point and the hot-update assembly.

    public static GameMain Instance { get; private set; }

    /// <summary>
    /// The currently active HotUpdateAssembly. Loaded once at startup from
    /// persistentCache > Resources, then optionally replaced by ApplyHotUpdate.
    /// Only used for types that remain in HotUpdateAssembly (HotUpdateStartupMacroCommand, LoginSuccessCommand, etc.).
    /// </summary>
    private Assembly _hotAssembly;

    // --- FrameworkAssembly types (AOT, resolved via Type.GetType) ---
    private Type _uiManagerType;
    private Type _hotUpdateManagerType;
    private Type _assetManagerType;
    private Type _assetBundleManagerType;
    private Type _networkManagerType;
    private Type _uiConstType;
    private Type _facadeType;
    private Type _hotUpdateUIMediatorType;
    private Type _uiLoginMediatorType;

    // --- Command types (all in FrameworkAssembly, REFRESHED after hot update if needed) ---
    private Type _cmdStartup;
    private Type _cmdHotUpdate;
    private Type _cmdLogin;
    private Type _cmdLoginSuccess;
    private Type _cmdRegister;
    private Type _cmdCreatePlayer;
    private Type _cmdNetworkDisconnected;
    private Type _cmdNetworkConnected;
    private Type _notifConstType;
    private Type _networkNotifConstType;

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

        // Fallback: load from Resources/HotUpdateAssembly.bytes (packaged by ProjectBuilder.
        // IL2CPP needs the DLL bytes to Assembly.Load because it doesn't expose
        // hot update assembly types via Type.GetType without an explicit load.)
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
        if (_hotAssembly == null)
        {
            Log.e("HotUpdateAssembly not loaded, cannot start", "GameMain");
            return;
        }

        CacheReflectedTypes();
        InitManagers();
        StartCoroutine(StartupFlow());
    }

    /// <summary>
    /// Cache all reflected types for performance.
    /// FrameworkAssembly types resolved via Type.GetType with assembly-qualified name.
    /// HotUpdateAssembly types (BagProxy etc.) resolved via _hotAssembly.
    /// Command/Const types are also in FrameworkAssembly now.
    /// </summary>
    void CacheReflectedTypes()
    {
        // FrameworkAssembly types (AOT, always available)
        _uiManagerType = ResolveFrameworkType("UIManager");
        _hotUpdateManagerType = ResolveFrameworkType("HotUpdateManager");
        _assetManagerType = ResolveFrameworkType("AssetManager");
        _assetBundleManagerType = ResolveFrameworkType("AssetBundleManager");
        _networkManagerType = ResolveFrameworkType("NetworkManager");
        _uiConstType = ResolveFrameworkType("UIConst");
        _facadeType = Type.GetType("PureMVC.Patterns.Facade.Facade,FrameworkAssembly")
                   ?? Type.GetType("PureMVC.Patterns.Facade.Facade");
        _hotUpdateUIMediatorType = ResolveFrameworkType("UIHotUpdateMediator");
        _uiLoginMediatorType = ResolveFrameworkType("UILoginMediator");

        // Cache command types (all in FrameworkAssembly)
        RefreshCommandTypes();
    }

    /// <summary>
    /// Resolve a type from FrameworkAssembly. Tries assembly-qualified first, then fallback.
    /// </summary>
    static Type ResolveFrameworkType(string typeName)
    {
        return Type.GetType(typeName + ",FrameworkAssembly")
            ?? Type.GetType(typeName);
    }

    /// <summary>
    /// (Re)cache command types. All are now in FrameworkAssembly.
    /// HotUpdateAssembly no longer contains commands/consts.
    /// </summary>
    void RefreshCommandTypes()
    {
        _cmdStartup = ResolveFrameworkType("StartupMacroCommand");
        _cmdHotUpdate = ResolveFrameworkType("HotUpdateCommand");
        _cmdLogin = ResolveFrameworkType("LoginCommand");
        _cmdLoginSuccess = _hotAssembly?.GetType("LoginSuccessCommand");
        _cmdRegister = ResolveFrameworkType("RegisterCommand");
        _cmdCreatePlayer = ResolveFrameworkType("CreatePlayerCommand");
        _cmdNetworkDisconnected = ResolveFrameworkType("NetworkDisconnectedCommand");
        _cmdNetworkConnected = ResolveFrameworkType("NetworkConnectedCommand");
        _notifConstType = ResolveFrameworkType("NotificationConst");
        _networkNotifConstType = ResolveFrameworkType("NetworkNotificationConst");

        Log.d("Command types refreshed (FrameworkAssembly + HotUpdateAssembly)", "GameMain");
    }

    /// <summary>
    /// Called by HotUpdateManager after a successful DLL download.
    /// IL2CPP does NOT support reloading an already-loaded assembly.
    /// Instead, we write the new DLL to the persistent cache so that
    /// the NEXT cold start picks it up via LoadBaseHotUpdateAssembly().
    /// Commands and managers are in FrameworkAssembly (AOT), so hot update
    /// only affects HotUpdateAssembly types (BagProxy, ShopProxy, UI mediators).
    /// </summary>
    public void ApplyHotUpdate(byte[] dllBytes)
    {
        if (dllBytes == null || dllBytes.Length == 0)
        {
            Log.e("ApplyHotUpdate: null or empty DLL bytes", "GameMain");
            return;
        }

        // Write new DLL to persistent cache for next cold start
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
    /// Get singleton instance via reflection.
    /// </summary>
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

    object GetInstance(Type type)
    {
        if (type == null) return null;
        var prop = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        return prop?.GetValue(null);
    }

    /// <summary>
    /// Call a method on an instance via reflection.
    /// Supports default parameter values and overload resolution.
    /// When no args passed, prefers parameterless overload.
    /// </summary>
    object CallMethod(object instance, string methodName, params object[] args)
    {
        if (instance == null) return null;
        var type = instance.GetType();
        MethodInfo method;
        if (args == null || args.Length == 0)
        {
            // Prefer parameterless overload
            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        }
        else
        {
            // Build type array from non-null args for overload resolution
            var argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i]?.GetType() ?? typeof(object);
            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, argTypes, null);
        }
        if (method == null)
        {
            // Fallback: try basic name-only search
            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        }
        if (method == null) return null;
        // Fill missing optional parameters with Missing.Value
        var parameters = method.GetParameters();
        if (args == null || args.Length < parameters.Length)
        {
            var filled = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < (args?.Length ?? 0))
                    filled[i] = args[i];
                else
                    filled[i] = Type.Missing;
            }
            return method.Invoke(instance, filled);
        }
        return method.Invoke(instance, args);
    }

    /// <summary>
    /// Call a static method via reflection.
    /// Supports default parameter values and overload resolution.
    /// When no args passed, prefers parameterless overload.
    /// </summary>
    object CallStaticMethod(Type type, string methodName, params object[] args)
    {
        if (type == null) return null;
        MethodInfo method;
        if (args == null || args.Length == 0)
        {
            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
        }
        else
        {
            var argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; i++)
                argTypes[i] = args[i]?.GetType() ?? typeof(object);
            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, argTypes, null);
        }
        if (method == null)
        {
            method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        }
        if (method == null) return null;
        var parameters = method.GetParameters();
        if (args == null || args.Length < parameters.Length)
        {
            var filled = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i < (args?.Length ?? 0))
                    filled[i] = args[i];
                else
                    filled[i] = Type.Missing;
            }
            return method.Invoke(null, filled);
        }
        return method.Invoke(null, args);
    }

    void InitManagers()
    {
        // Trigger singleton initialization for all managers (all in FrameworkAssembly)
        GetInstance(_uiManagerType);
        GetInstance(_hotUpdateManagerType);
        GetInstance(_assetManagerType);
        GetInstance(_assetBundleManagerType);
        GetInstance(_networkManagerType);

        // Other FrameworkAssembly managers
        var updateManagerType = ResolveFrameworkType("UpdateManager");
        var timerManagerType = ResolveFrameworkType("TimerManager");
        var audioManagerType = ResolveFrameworkType("AudioManager");
        var objectPoolManagerType = ResolveFrameworkType("ObjectPoolManager");
        var gameSceneManagerType = ResolveFrameworkType("GameSceneManager");
        var saveManagerType = ResolveFrameworkType("SaveManager");
        var configManagerType = ResolveFrameworkType("ConfigManager");

        GetInstance(updateManagerType);
        GetInstance(timerManagerType);
        GetInstance(audioManagerType);
        GetInstance(objectPoolManagerType);
        GetInstance(gameSceneManagerType);
        GetInstance(saveManagerType);
        GetInstance(configManagerType);

        // UIManager.Init()
        var uiMgr = GetInstance(_uiManagerType);
        CallMethod(uiMgr, "Init");

        // AssetBundleManager.Initialize("HotUpdate")
        // Skip in Editor — AssetDatabase provides assets directly, no AB needed
#if !UNITY_EDITOR
        var abMgr = GetInstance(_assetBundleManagerType);
        CallMethod(abMgr, "Initialize", "HotUpdate");
#endif

        Log.d("All managers initialized", "GameMain");
    }

    IEnumerator StartupFlow()
    {
        Log.d("Starting hot update check...", "GameMain");

        // HotUpdateManager.Initialize()
        var hotUpdateMgr = GetInstance(_hotUpdateManagerType);
        CallMethod(hotUpdateMgr, "Initialize");

        // Init, GameStart sends STARTUP → HotUpdateCommand → check → UI or success
        InitModule();
        GameStart();  // This sends STARTUP → StartupMacroCommand → HotUpdateCommand
        ConnectServer();

        // Wait for hot update to complete (HotUpdateCommand sets HotUpdateManager.State)
#if UNITY_EDITOR
        // Editor: skip wait
#else
        float timeout = 30f;
        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            var state = CallMethod(hotUpdateMgr, "get_State");
            if (state != null)
            {
                int stateVal = System.Convert.ToInt32(state);
                if (stateVal == 4 || stateVal == 5 || elapsed > timeout) break;
            }
            yield return null;
        }
#endif

#if !UNITY_EDITOR
        // Reload AB manifest so newly downloaded bundles are available
        var abMgr = GetInstance(_assetBundleManagerType);
        CallMethod(abMgr, "ReloadManifest");

        // AssetManager.SetAssetLoader(HotUpdateManager.Instance.AssetLoader)
        var assetMgr = GetInstance(_assetManagerType);
        var assetLoaderProp = _hotUpdateManagerType?.GetProperty("AssetLoader", BindingFlags.Public | BindingFlags.Instance);
        var assetLoader = assetLoaderProp?.GetValue(hotUpdateMgr);
        CallMethod(assetMgr, "SetAssetLoader", assetLoader);
#endif

        // Open login only if no restart is needed.
        // When a hot update download succeeds, HotUpdateManager.NeedRestart is true
        // and RestartCommand has already opened the restart UI — skip login.
        bool needRestart = false;
        var needRestartProp = _hotUpdateManagerType?.GetProperty("NeedRestart", BindingFlags.Public | BindingFlags.Instance);
        if (needRestartProp != null)
        {
            var val = needRestartProp.GetValue(hotUpdateMgr);
            if (val is bool b) needRestart = b;
        }

        if (!needRestart)
        {
            OpenLogin();
        }
        yield break;
    }

    void InitModule()
    {
        // Init Framework UIConst (instance singleton)
        var uiConst = GetInstance(_uiConstType);
        CallMethod(uiConst, "Init");

        // Register hot-update UI definitions via reflection
        var hotUpdateUIConstType = _hotAssembly?.GetType("HotUpdateUIConst");
        if (hotUpdateUIConstType != null)
        {
            var registerToMethod = hotUpdateUIConstType.GetMethod("RegisterTo", BindingFlags.Public | BindingFlags.Static);
            registerToMethod?.Invoke(null, new object[] { uiConst });
            Log.d("HotUpdateUIConst registered", "GameMain");
        }

        // Register hot-update consts via reflection (all no-arg RegisterTo methods)
        InvokeHotUpdateConst("HotUpdateNotificationConst");
        InvokeHotUpdateConst("HotUpdateMessageConst");
        InvokeHotUpdateConst("HotUpdateProxyConst");
        InvokeHotUpdateConst("HotUpdatePlayerPrefsConst");

        // Initialize Lua subsystem (LuaBootstrap is in FrameworkAssembly)
        var luaBootstrapType = ResolveFrameworkType("LuaBootstrap");
        if (luaBootstrapType != null)
        {
            var luaBootstrap = GetInstance(luaBootstrapType);
            CallMethod(luaBootstrap, "Initialize");
        }
    }

    void GameStart()
    {
        var facade = GetInstance(_facadeType);
        if (facade == null)
        {
            Log.e("Failed to get PureMVC Facade instance", "GameMain");
            return;
        }

        var registerCmdMethod = _facadeType?.GetMethod("RegisterCommand");
        var sendNotifMethod = _facadeType?.GetMethod("SendNotification");

        // Command types are in FrameworkAssembly
        var startupType = _cmdStartup;
        var hotUpdateCmdType = _cmdHotUpdate;
        var loginCmdType = _cmdLogin;
        var loginSuccessCmdType = _cmdLoginSuccess;
        var registerCmdType = _cmdRegister;
        var createPlayerCmdType = _cmdCreatePlayer;
        var networkDiscCmdType = _cmdNetworkDisconnected;
        var networkConnCmdType = _cmdNetworkConnected;

        var notifConstType = _notifConstType;
        var networkNotifConstType = _networkNotifConstType;

        string STARTUP = GetStaticField<string>(notifConstType, "STARTUP") ?? "STARTUP";
        string HOT_UPDATE_CHECK = GetStaticField<string>(notifConstType, "HOT_UPDATE_CHECK") ?? "HOT_UPDATE_CHECK";
        string LOGIN = GetStaticField<string>(notifConstType, "LOGIN") ?? "LOGIN";
        string LOGIN_SUCCESS = GetStaticField<string>(notifConstType, "LOGIN_SUCCESS") ?? "LOGIN_SUCCESS";
        string REGISTER = GetStaticField<string>(notifConstType, "REGISTER") ?? "REGISTER";
        string CREATE_PLAYER = GetStaticField<string>(notifConstType, "CREATE_PLAYER") ?? "CREATE_PLAYER";
        string NETWORK_DISCONNECTED = GetStaticField<string>(networkNotifConstType, "NETWORK_DISCONNECTED") ?? "NETWORK_DISCONNECTED";
        string NETWORK_CONNECTED = GetStaticField<string>(networkNotifConstType, "NETWORK_CONNECTED") ?? "NETWORK_CONNECTED";

        RegisterCommand(facade, registerCmdMethod, STARTUP, startupType);
        RegisterCommand(facade, registerCmdMethod, HOT_UPDATE_CHECK, hotUpdateCmdType);
        RegisterCommand(facade, registerCmdMethod, LOGIN, loginCmdType);
        RegisterCommand(facade, registerCmdMethod, LOGIN_SUCCESS, loginSuccessCmdType);
        RegisterCommand(facade, registerCmdMethod, REGISTER, registerCmdType);
        RegisterCommand(facade, registerCmdMethod, CREATE_PLAYER, createPlayerCmdType);
        RegisterCommand(facade, registerCmdMethod, NETWORK_DISCONNECTED, networkDiscCmdType);
        RegisterCommand(facade, registerCmdMethod, NETWORK_CONNECTED, networkConnCmdType);

        // Execute hot-update startup macro command (registers hot-update commands + proxies)
        var hotStartupCmdType = _hotAssembly?.GetType("HotUpdateStartupMacroCommand");
        if (hotStartupCmdType != null)
        {
            var hotStartupCmd = Activator.CreateInstance(hotStartupCmdType);
            var executeMethod = hotStartupCmdType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);
            if (executeMethod != null)
            {
                var notifConstructor = Type.GetType("PureMVC.Patterns.Observer.Notification,FrameworkAssembly")
                    ?.GetConstructor(new[] { typeof(string), typeof(object), typeof(string) });
                var notif = notifConstructor?.Invoke(new object[] { "HOT_UPDATE_STARTUP", null, null });
                executeMethod.Invoke(hotStartupCmd, new[] { notif });
                Log.d("HotUpdateStartupMacroCommand executed", "GameMain");
            }
        }

        sendNotifMethod?.Invoke(facade, new object[] { STARTUP, null, null });
    }

    T GetStaticField<T>(Type type, string fieldName)
    {
        if (type == null) return default;
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        if (field != null && field.GetValue(null) is T val)
            return val;
        return default;
    }

    void RegisterCommand(object facade, MethodInfo registerMethod, string notifName, Type cmdType)
    {
        if (registerMethod == null || cmdType == null) return;
        var ctor = cmdType.GetConstructor(Type.EmptyTypes);
        if (ctor == null) return;
        // Create Func<ICommand> that calls the constructor
        var iCommandType = Type.GetType("PureMVC.Interfaces.ICommand,FrameworkAssembly")
                        ?? Type.GetType("PureMVC.Interfaces.ICommand")
                        ?? Type.GetType("PureMVC.Interfaces.ICommand,PureMVCFramework");
        var funcType = typeof(Func<>).MakeGenericType(iCommandType);
        var func = Delegate.CreateDelegate(funcType, null,
            typeof(GameMain).GetMethod(nameof(CreateCommandInstance), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(cmdType));
        registerMethod.Invoke(facade, new object[] { notifName, func });
    }

    static T CreateCommandInstance<T>() where T : new() => new T();

    void ConnectServer()
    {
        var netMgr = GetInstance(_networkManagerType);
        CallMethod(netMgr, "Connect");
    }

    void OpenLogin()
    {
        var uiMgr = GetInstance(_uiManagerType);
        // OpenUI<T> is a generic method — must bind T via MakeGenericMethod
        var openUIMethodDef = _uiManagerType?.GetMethod("OpenUI");
        var openUIMethod = openUIMethodDef?.MakeGenericMethod(_uiLoginMediatorType);
        string uiLoginName = GetStaticField<string>(_uiConstType, "UILogin") ?? "UILogin";
        // OpenUI<T>(string uiName, EUILayer layer, bool isPushStack, bool hideLastUI) — use defaults
        openUIMethod?.Invoke(uiMgr, new object[] { uiLoginName, Type.Missing, Type.Missing, Type.Missing });
    }
}
