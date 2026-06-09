using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using PureMVC.Patterns.Facade;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public enum EUILayer
{
    MainLayer = 1,
    FirstLayer = 2,
    SecondLayer = 3,
    ThirdLayer = 4,
    FourthLayer = 5,
    GuideLayer = 6
}


public class UIManager
{
    #region Singleton
    private static UIManager instance;
    private static readonly object _instanceLock = new object();
    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                lock (_instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new UIManager();
                    }
                }
            }
            return instance;
        }
    }

    private UIManager()
    {
        layerTransPath = new Dictionary<EUILayer, string>();
        layerTransPath[EUILayer.MainLayer] = UIConst.MAIN_CANVAS;
        layerTransPath[EUILayer.FirstLayer] = UIConst.FIRST_CANVAS;
        layerTransPath[EUILayer.SecondLayer] = UIConst.SECOND_CANVAS;
        layerTransPath[EUILayer.ThirdLayer] = UIConst.THIRD_CANVAS;
        layerTransPath[EUILayer.FourthLayer] = UIConst.FOURTH_CANVAS;
        layerTransPath[EUILayer.GuideLayer] = UIConst.GUIDE_CANVAS;
        layerTransDic = new Dictionary<EUILayer, Transform>();
        uiMediatorDic = new Dictionary<string, UIMediatorBase>();
        // InitUIRoot is NOT called here intentionally.
        // Call UIManager.Instance.Init() explicitly in GameMain.Start()
        // to guarantee the scene (UIRoot node) is fully loaded first.
    }
    #endregion

    #region Member Variables
    private Transform uiRoot;
    private Dictionary<EUILayer, Transform> layerTransDic;
    private Dictionary<EUILayer, string> layerTransPath;
    private Dictionary<string, UIMediatorBase> uiMediatorDic;
    private Stack<string> uiStack = new Stack<string>();
    private Facade pureMvcFacade => Facade.Instance;
    #endregion

    #region Initialization
    private bool isInitialized = false;

    /// <summary>
    /// Must be called once in GameMain.Start() after the scene is ready.
    /// </summary>
    public void Init()
    {
        if (isInitialized)
        {
            Log.w("UIManager.Init() called more than once", "UIManager");
            return;
        }
        InitUIRoot();
        isInitialized = true;
        Log.d("UIManager initialized", "UIManager");
    }

    private void InitUIRoot()
    {
        var uiRootGo = GameObject.Find(UIConst.UI_ROOT_PATH);
        if (uiRootGo == null)
        {
            Log.e("UIRoot node not found");
            return;
        }
        var trans = uiRootGo.transform;
        foreach (var item in layerTransPath)
        {
            var targetTrans = trans.Find(item.Value);
            if (targetTrans == null)
            {
                Log.w($"Layer not found, skipped: {item.Value}");
                continue;
            }
            layerTransDic[item.Key] = targetTrans;
        }
        // Fatal: MainLayer must exist for UI system to function
        if (!layerTransDic.ContainsKey(EUILayer.MainLayer))
        {
            Log.e("MainLayer not found, UI system cannot function");
            return;
        }
        uiRoot = trans;
        UnityEngine.Object.DontDestroyOnLoad(uiRoot.parent);
    }
    #endregion

    #region Create Mediator
    // Cache reflected ConstructorInfo to avoid repeated Activator.CreateInstance type-resolution overhead.
    // IL2CPP-friendly: uses ConstructorInfo.Invoke (always supported), no Expression.Compile.
    // Key: mediator type, Value: cached ctor (string, GameObject, int, bool)
    // ConcurrentDictionary for thread-safe reads (ctor resolution is idempotent).
    private static readonly ConcurrentDictionary<Type, System.Reflection.ConstructorInfo> _mediatorCtorCache
        = new ConcurrentDictionary<Type, System.Reflection.ConstructorInfo>();

    private static System.Reflection.ConstructorInfo GetMediatorCtor(Type type)
    {
        return _mediatorCtorCache.GetOrAdd(type, t =>
        {
            var ctor = t.GetConstructor(new[] { typeof(string), typeof(GameObject), typeof(int), typeof(bool) });
            if (ctor == null)
                Log.e($"Mediator type {t.Name} missing required constructor (string,GameObject,int,bool)", "UIManager");
            return ctor;
        });
    }
    #endregion

    #region Open UI
    /// <summary>
    /// Open a UI that creates its own GameObject (no prefab needed).
    /// Used for dynamically-created UIs like HotUpdateUI.
    /// </summary>
    public void OpenUIWithoutPrefab<T>(string uiName, EUILayer layer = EUILayer.MainLayer) where T : UIMediatorBase
    {
        if (!isInitialized)
        {
            Log.e("UIManager.OpenUIWithoutPrefab called before Init()", "UIManager");
            return;
        }
        if (uiMediatorDic.ContainsKey(uiName))
        {
            uiMediatorDic[uiName].Show();
            return;
        }

        var parentTrans = layerTransDic[layer];
        GameObject uiGo = new GameObject(uiName);
        uiGo.transform.SetParent(parentTrans, false);

        TryCreateAndRegisterMediator<T>(uiName, uiGo, layer, isPushStack: true);
    }

    public void OpenUI<T>(string uiName, EUILayer layer = EUILayer.MainLayer, bool isPushStack = true, bool hideLastUI = true) where T : UIMediatorBase
    {
        if (!isInitialized)
        {
            Log.e("UIManager.OpenUI called before Init(). Call UIManager.Instance.Init() in GameMain.Start() first.", "UIManager");
            return;
        }
        var uiViewDefData = UIConst.Instance.GetUIViewDef(uiName);
        if (uiViewDefData == null)
        {
            return;
        }
        if (uiMediatorDic.ContainsKey(uiName))
        {
            // UI already exists, just show it without touching the stack
            uiMediatorDic[uiName].Show();
            return;
        }

        if (hideLastUI && uiStack.Count > 0)
        {
            string lastUiName = uiStack.Peek();
            if (uiMediatorDic.ContainsKey(lastUiName))
            {
                uiMediatorDic[lastUiName].Hide();
            }
        }

        var prefabPath = uiViewDefData.rootPath + uiViewDefData.prefabPath;
        GameObject uiPrefab = AssetManager.Instance.LoadAsset<GameObject>(prefabPath);
        if (uiPrefab == null)
        {
            Log.e($"UI prefab does not exist: {prefabPath}", "UIManager");
            return;
        }
        var parentTrans = layerTransDic[layer];
        GameObject uiGo = GameObject.Instantiate(uiPrefab, parentTrans);
        uiGo.name = uiName;

        TryCreateAndRegisterMediator<T>(uiName, uiGo, layer, isPushStack);
    }

    /// <summary>
    /// Create mediator via cached ctor, register with Facade, add to dictionary, optionally push stack, then Show.
    /// </summary>
    private void TryCreateAndRegisterMediator<T>(string uiName, GameObject uiGo, EUILayer layer, bool isPushStack) where T : UIMediatorBase
    {
        T uiMediator = null;
        try
        {
            var ctor = GetMediatorCtor(typeof(T));
            if (ctor != null)
                uiMediator = (T)ctor.Invoke(new object[] { uiName, uiGo, (int)layer, false });
        }
        catch (Exception e)
        {
            Log.e($"Failed to create mediator for {uiName}: {e.Message}", "UIManager");
        }
        if (uiMediator == null)
        {
            Log.e($"CreateMediator failed for: {uiName}", "UIManager");
            GameObject.Destroy(uiGo);
            return;
        }

        pureMvcFacade.RegisterMediator(uiMediator);

        uiMediatorDic.Add(uiName, uiMediator);
        if (isPushStack)
        {
            uiStack.Push(uiName);
        }

        uiMediator.Show();
    }
    #endregion

    #region Close UI
    public void CloseUI(string uiName, bool showLastUI = true)
    {
        if (!uiMediatorDic.ContainsKey(uiName))
        {
            Log.w($"Close failed, UI not opened: {uiName}", "UIManager");
            return;
        }

        UIMediatorBase mediator = uiMediatorDic[uiName];
        mediator.Close();
        pureMvcFacade.RemoveMediator(uiName);
        uiMediatorDic.Remove(uiName);

        if (uiStack.Contains(uiName))
        {
            // Collect items above target without allocating a temp Stack
            var tempList = new List<string>();
            while (uiStack.Count > 0)
            {
                string top = uiStack.Pop();
                if (top == uiName) break;
                tempList.Add(top);
            }
            for (int i = tempList.Count - 1; i >= 0; i--)
                uiStack.Push(tempList[i]);
        }

        if (showLastUI && uiStack.Count > 0)
        {
            string last = uiStack.Peek();
            if (uiMediatorDic.ContainsKey(last))
            {
                uiMediatorDic[last].Show();
            }
        }
    }

    public void CloseCurrentUI()
    {
        if (uiStack.Count == 0) return;
        CloseUI(uiStack.Peek());
    }

    public void CloseAllUI()
    {
        // Snapshot to avoid collection-modified-during-enumeration
        var mediators = new List<UIMediatorBase>(uiMediatorDic.Values);
        foreach (var m in mediators)
        {
            m.Close();
            pureMvcFacade.RemoveMediator(m.MediatorName);
        }
        uiMediatorDic.Clear();
        uiStack.Clear();
    }
    #endregion

    #region Loading
    /// <summary>
    /// Show a reusable full-screen loading overlay.
    /// Does NOT push to uiStack — it lives on the topmost layer and does not
    /// interfere with normal UI navigation (show/hide/push/pop).
    /// Idempotent: repeated calls just show the same instance.
    /// </summary>
    public void ShowLoading()
    {
        OpenUI<UILoadingMediator>(UIConst.UILoading, EUILayer.SecondLayer, isPushStack: false, hideLastUI: false);
    }

    /// <summary>
    /// Hide the loading overlay if it is currently open.
    /// Safe to call when loading is not showing.
    /// </summary>
    public void HideLoading()
    {
        if (uiMediatorDic.TryGetValue(UIConst.UILoading, out var mediator))
        {
            mediator.Hide();
        }
    }
    #endregion

    #region Helper Methods
    public T GetUIMediator<T>(string uiName) where T : UIMediatorBase
    {
        if (uiMediatorDic.TryGetValue(uiName, out UIMediatorBase m))
        {
            return m as T;
        }
        return null;
    }

    public bool IsUIOpen(string uiName)
    {
        return uiMediatorDic.ContainsKey(uiName);
    }

    public string GetCurrentUIName()
    {
        return uiStack.Count > 0 ? uiStack.Peek() : string.Empty;
    }
    #endregion
}