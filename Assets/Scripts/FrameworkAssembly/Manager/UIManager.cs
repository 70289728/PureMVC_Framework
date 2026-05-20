using System;
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
    ForthLayer = 5,
    GuideLayer = 6
}


public class UIManager
{
    #region Singleton
    private static UIManager instance;
    public static UIManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new UIManager();
                //GameObject obj = new GameObject("UIManager");
                //instance = obj.AddComponent<UIManager>();
                //DontDestroyOnLoad(obj);
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
        layerTransPath[EUILayer.ForthLayer] = UIConst.FORTH_CANVAS;
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
                Log.e($"Layer not found: {item.Value}");
                return;
            }
            layerTransDic[item.Key] = targetTrans;
        }
        uiRoot = trans;
        UnityEngine.Object.DontDestroyOnLoad(uiRoot.parent);
    }
    #endregion

    #region Create Mediator
    private UIMediatorBase CreateMediator(string uiName, GameObject viewObj, int layer)
    {
        // Resolve mediator type by convention: "UI{Name}Mediator"
        // Search all loaded assemblies (covers both FrameworkAssembly and HotUpdateAssembly)
        var type = FindMediatorType(uiName);
        if (type == null)
        {
            Log.w($"Mediator type not found for UI: {uiName}", "UIManager");
            return null;
        }
        try
        {
            return (UIMediatorBase)Activator.CreateInstance(type, uiName, viewObj, layer, false);
        }
        catch (Exception e)
        {
            Log.e($"Failed to create mediator for {uiName}: {e.Message}", "UIManager");
            return null;
        }
    }

    private static Type FindMediatorType(string uiName)
    {
        // Convention: "UILogin" → "UILoginMediator"
        string typeName = uiName + "Mediator";
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType(typeName);
            if (type != null && typeof(UIMediatorBase).IsAssignableFrom(type))
                return type;
        }
        return null;
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

        // Create mediator using the compile-time generic type T.
        // Do NOT call CreateMediator() here — it uses AppDomain.GetAssemblies()
        // which fails on IL2CPP where assemblies are merged.
        T uiMediator = null;
        try
        {
            uiMediator = (T)Activator.CreateInstance(typeof(T), uiName, uiGo, (int)layer, false);
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
        uiStack.Push(uiName);
        uiMediator.Show();
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
        var prefebPath = uiViewDefData.prefabPath;
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
        //UnityEngine.Object.DontDestroyOnLoad(uiGo);
        // Create mediator using the compile-time generic type T (avoids
        // AppDomain.GetAssemblies() reflection which fails on IL2CPP).
        T uiMediator = null;
        try
        {
            uiMediator = (T)Activator.CreateInstance(typeof(T), uiName, uiGo, (int)layer, false);
        }
        catch (Exception e)
        {
            Log.e($"CreateMediator failed for: {uiName}: {e.Message}", "UIManager");
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
            Stack<string> tempStack = new Stack<string>();
            while (uiStack.Count > 0)
            {
                string top = uiStack.Pop();
                if (top == uiName) break;
                tempStack.Push(top);
            }
            while (tempStack.Count > 0)
            {
                uiStack.Push(tempStack.Pop());
            }
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
        foreach (var m in uiMediatorDic.Values)
        {
            m.Close();
            pureMvcFacade.RemoveMediator(m.MediatorName);
        }
        uiMediatorDic.Clear();
        uiStack.Clear();
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