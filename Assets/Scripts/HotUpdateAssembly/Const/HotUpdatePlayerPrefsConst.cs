/// <summary>
/// Hot-update PlayerPrefs keys.
/// RegisterTo() is called by GameMain at startup after FrameworkAssembly constants are ready.
/// </summary>
public static class HotUpdatePlayerPrefsConst
{
    /// <summary>
    /// Register hot-update-specific keys. Called by GameMain.InitModule via reflection.
    /// </summary>
    public static void RegisterTo()
    {
        // Register hot-update PlayerPrefs keys here when needed.
        // Example: PlayerPrefsManager.RegisterKey("pf_shop_filter");
    }
}
