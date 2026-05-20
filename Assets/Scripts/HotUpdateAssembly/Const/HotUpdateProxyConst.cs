/// <summary>
/// Hot-update ProxyConst registrations.
/// Called by GameMain.InitModule() at startup to merge into FrameworkAssembly's ProxyConst.
/// 
/// Add new proxy name mappings for hot-update features here.
/// </summary>
public static class HotUpdateProxyConst
{
    #region Proxy Name Constants
    // Add hot-update proxy names here, e.g.:
    // public const string XXX_PROXY = "XxxProxy";
    #endregion

    /// <summary>
    /// Register all hot-update proxy names into FrameworkAssembly's ProxyConst.
    /// Called via reflection from GameMain.InitModule().
    /// </summary>
    public static void RegisterTo()
    {
        // Add hot-update proxy registrations here, e.g.:
        // ProxyConst.Register("XXX_PROXY", XXX_PROXY);
    }
}
