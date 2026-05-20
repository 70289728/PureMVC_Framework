/// <summary>
/// UI name constants and view-definition registration for hot-updatable UIs.
/// HotUpdateAssembly code references constants here (not FrameworkAssembly's UIConst).
/// Registration into FrameworkAssembly's UIConst happens at startup via reflection.
/// </summary>
public static class HotUpdateUIConst
{
    #region UI Name Constants (HotUpdateAssembly)
    public const string UIMain = "UIMain";
    public const string UICreatePlayer = "UICreatePlayer";
    #endregion

    #region Prefab Root Path
    public const string UI_ROOT = "Assets/ProjectAssets/HotUpdate/UIAssets/Prefabs/";
    #endregion

    /// <summary>
    /// Register all hot-update UI definitions into FrameworkAssembly's UIConst.
    /// Called via reflection from GameMain.InitModule().
    /// </summary>
    public static void RegisterTo(UIConst uiConst)
    {
        if (uiConst == null) return;

        uiConst.RegisterUI(UIMain, "UIMain/UIMainPanel.prefab", UI_ROOT);
        uiConst.RegisterUI(UICreatePlayer, "UICreatePlayer/UICreatePlayerPanel.prefab", UI_ROOT);
    }
}
