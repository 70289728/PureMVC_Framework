using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIConst
{
    #region Singleton
    private static UIConst instance;
    public static UIConst Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new UIConst();
            }
            return instance;
        }
    }
    private UIConst()
    {
        uIViewDefs = new Dictionary<string, UIViewDef>();
    }
    #endregion

    #region UI Name Constants (Base layer — FrameworkAssembly)
    public const string UITips = "UITips";
    public const string UIReStart = "UIReStart";
    public const string UILogin = "UILogin";
    public const string UIHotUpdate = "UIHotUpdate";
    public const string UIShop = "UIShop";
    public const string UIBag = "UIBag";
    #endregion

    #region Prefab Root Paths
    public const string UI_PREFAB_ROOT_BASE = "Assets/ProjectAssets/Base/UIAssets/Prefabs/";
    public const string UI_PREFAB_ROOT_HOTUPDATE = "Assets/ProjectAssets/HotUpdate/UIAssets/Prefabs/";
    #endregion

    #region UI Root Paths
    public const string UI_ROOT_PATH = "UIRoot/Root";
    public const string MAIN_CANVAS = "MainCanvas";
    public const string FIRST_CANVAS = "FirstCanvas";
    public const string SECOND_CANVAS = "SecondCanvas";
    public const string THIRD_CANVAS = "ThirdCanvas";
    public const string FORTH_CANVAS = "FourthCanvas";
    public const string GUIDE_CANVAS = "GuideCanvas";
    #endregion

    private Dictionary<string, UIViewDef> uIViewDefs;
    private bool isInit = false;

    public void Init()
    {
        if (isInit)
        {
            Log.w("UIConst.Init() called more than once", "UIConst");
            return;
        }

        // Framework (Base) UIs
        RegisterUI(UILogin, "UILogin/UILoginPanel.prefab", UI_PREFAB_ROOT_BASE);
        RegisterUI(UIReStart, "UIReStart/UIReStartPanel.prefab", UI_PREFAB_ROOT_BASE);

        RegisterUI(UITips, "UITips/UITipsPanel.prefab", UI_PREFAB_ROOT_BASE);


        RegisterUI(UIHotUpdate, "UIHotUpdate/UIHotUpdatePanel.prefab", UI_PREFAB_ROOT_BASE);

        RegisterUI(UIShop, "UIShop/UIShopPanel.prefab", UI_PREFAB_ROOT_HOTUPDATE);
        RegisterUI(UIBag, "UIBag/UIBagPanel.prefab", UI_PREFAB_ROOT_HOTUPDATE);

        isInit = true;
        Log.d("UIConst initialized", "UIConst");
    }

    /// <summary>
    /// Register a UI definition. HotUpdateAssembly calls this via reflection
    /// to merge its UI definitions at startup.
    /// </summary>
    public void RegisterUI(string uiName, string prefabPath, string rootPath)
    {
        if (uIViewDefs.ContainsKey(uiName))
        {
            Log.w($"UIConst.RegisterUI: duplicate UI name '{uiName}' — overwriting", "UIConst");
        }
        uIViewDefs[uiName] = new UIViewDef(prefabPath, rootPath);
    }

    public UIViewDef GetUIViewDef(string uiName)
    {
        if (string.IsNullOrEmpty(uiName))
        {
            Log.e("UIConst: UI name is empty", "UIConst");
            return null;
        }
        if (uIViewDefs.TryGetValue(uiName, out var viewDef))
        {
            return viewDef;
        }
        Log.e($"UIConst: No definition found for UI: {uiName}", "UIConst");
        return null;
    }
}

public class UIViewDef
{
    public string prefabPath;
    public string rootPath;

    public UIViewDef(string path, string root)
    {
        prefabPath = path;
        rootPath = root;
    }
}
