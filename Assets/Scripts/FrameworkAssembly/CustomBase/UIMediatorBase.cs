using PureMVC.Patterns.Mediator;
using PureMVC.Patterns.Facade;
using UnityEngine;

public abstract class UIMediatorBase : Mediator, IUpdatable
{
    protected GameObject viewRootGo;
    protected Transform viewTrans;
    protected CanvasGroup canvasGroup;
    protected bool isShowing = false;
    protected bool isEventsRegistered = false;

    // True only after the constructor finishes. Used to skip side-effects in initial Hide().
    private bool _constructed = false;

    /// <summary>
    /// Retrieve a registered proxy by name.
    /// </summary>
    protected T GetProxy<T>(string proxyName) where T : ProxyBase
    {
        return Facade.RetrieveProxy(proxyName) as T;
    }
    protected bool isReuseView = false;
    protected int viewLayer = 0;

    // IUpdatable implementation
    public bool IsUpdateActive => isShowing && NeedsUpdate();

    protected UIMediatorBase(string mediatorName, GameObject viewComponent, int layer, bool isReuseView = false) : base(mediatorName, viewComponent)
    {
        viewRootGo = viewComponent;
        viewTrans = viewComponent.transform;
        canvasGroup = viewRootGo.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = viewRootGo.AddComponent<CanvasGroup>();
        this.isReuseView = isReuseView;
        viewLayer = layer;
        InitUIComponents();
        // Note: Don't register to UpdateManager here, will register when Show() is called
        Hide();
        _constructed = true;
    }

    /// <summary>
    /// Override this to specify if this mediator needs update functionality
    /// Default is false to avoid unnecessary performance cost
    /// </summary>
    protected virtual bool NeedsUpdate() => false;

    /// <summary>
    /// Override this to specify update frequency
    /// Only called when NeedsUpdate() returns true
    /// </summary>
    protected virtual UpdateFrequency GetUpdateFrequency() => UpdateFrequency.EveryFrame;

    /// <summary>
    /// Override this to specify update types
    /// Only called when NeedsUpdate() returns true
    /// </summary>
    protected virtual UpdateType[] GetUpdateTypes() => new UpdateType[] { UpdateType.Update };

    /// <summary>
    /// Register to UpdateManager if needed
    /// </summary>
    private void RegisterToUpdateManager()
    {
        if (NeedsUpdate())
        {
            var updateTypes = GetUpdateTypes();
            var frequency = GetUpdateFrequency();
            foreach (var type in updateTypes)
            {
                UpdateManager.Instance.Register(this, type, frequency);
            }
            Log.d($"{MediatorName} registered to UpdateManager", "UIMediatorBase");
        }
    }

    /// <summary>
    /// Unregister from UpdateManager
    /// </summary>
    private void UnregisterFromUpdateManager()
    {
        if (NeedsUpdate())
        {
            var updateTypes = GetUpdateTypes();
            foreach (var type in updateTypes)
            {
                UpdateManager.Instance.Unregister(this, type);
            }
            Log.d($"{MediatorName} unregistered from UpdateManager", "UIMediatorBase");
        }
    }

    /// <summary>
    /// Find a UI component by its BindKey. Searches all IUIBind components in the view hierarchy.
    /// Returns null if not found or type mismatch.
    /// Lua can call this via mediator:FindComponentByBindKey("Button_CloseBtn").
    /// </summary>
    public T FindComponentByBindKey<T>(string bindKey) where T : Component
    {
        var binds = viewTrans.GetComponentsInChildren<IUIBind>(true);
        foreach (var bind in binds)
        {
            if (bind.BindKey == bindKey && bind.BoundComponent is T comp)
                return comp;
        }
        return null;
    }

    protected virtual void InitUIComponents()
    {

    }

    protected virtual void RegisterUIEvents()
    {
        isEventsRegistered = true;
    }

    protected virtual void UnRegisterUIEvents()
    {
        isEventsRegistered = false;
    }

    // IUpdatable interface implementation
    public virtual void OnUpdate(float deltaTime)
    {
        // Override in subclass if needed
    }

    public virtual void OnFixedUpdate(float fixedDeltaTime)
    {
        // Override in subclass if needed
    }

    public virtual void OnLateUpdate(float deltaTime)
    {
        // Override in subclass if needed
    }

    /// <summary>
    /// Try invoke a Lua hook for the given hook name.
    /// Lua file path: LuaScripts/MediatorHook/{mediatorTypeName}.lua
    /// Lua function name matches hookName.
    /// Returns true if Lua handled it.
    /// Returns false if LuaBootstrap is not initialized (e.g. during construction).
    /// </summary>
    protected bool TryLuaHook(string hookName, params object[] args)
    {
        // Guard: LuaBootstrap may not be initialized during construction
        if (LuaBootstrap.Instance == null || !LuaBootstrap.Instance.IsInitialized)
            return false;
        return LuaHookHelper.TryLuaHook("MediatorHook", GetType().Name, hookName, args);
    }

    public virtual void OnShow()
    {

    }

    public virtual void Show()
    {
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        isShowing = true;
        if (!viewRootGo.activeSelf)
            viewRootGo.SetActive(true);
        
        // Register UI events
        if (!isEventsRegistered)
        {
            RegisterUIEvents();
        }
        
        // Register to UpdateManager when showing
        RegisterToUpdateManager();
        
        // Try Lua hook first; if Lua handles it, skip C# OnShow
        if (!TryLuaHook("OnShow"))
            OnShow();
    }


    public virtual void OnHide()
    {

    }

    public virtual void Hide()
    {
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        isShowing = false;

        // Skip side-effects during construction: not registered yet, no Lua available.
        if (!_constructed) return;

        // Unregister from UpdateManager when hiding
        UnregisterFromUpdateManager();
        
        // Try Lua hook first; if Lua handles it, skip C# OnHide
        if (!TryLuaHook("OnHide"))
            OnHide();
    }

    public virtual void OnClose()
    {

    }

    public virtual void Close()
    {
        // Try Lua hook first; if Lua handles it, skip C# OnClose
        if (!TryLuaHook("OnClose"))
            OnClose();
        UnRegisterUIEvents();
        UnregisterFromUpdateManager();

        if (isReuseView)
        {
            ObjectPoolManager.Instance.Recycle(viewRootGo.name, viewRootGo);
        }
        else
        {
            GameObject.Destroy(viewRootGo);
        }
    }

    public override void OnRemove()
    {
        base.OnRemove();
        if (isEventsRegistered)
        {
            UnRegisterUIEvents();
        }
        UnregisterFromUpdateManager();
        Hide();
        Log.d($"{MediatorName} Mediator removed successfully", "UIMediatorBase");
    }
}