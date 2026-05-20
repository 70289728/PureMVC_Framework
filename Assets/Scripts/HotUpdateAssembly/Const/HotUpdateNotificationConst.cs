/// <summary>
/// Hot-update NotificationConst registrations.
/// Called by GameMain.InitModule() at startup to merge into FrameworkAssembly's NotificationConst.
/// 
/// Add new notification names for hot-update features here.
/// </summary>
public static class HotUpdateNotificationConst
{
    /// <summary>
    /// Register all hot-update notification names into FrameworkAssembly's NotificationConst.
    /// Called via reflection from GameMain.InitModule().
    /// </summary>
    public static void RegisterTo()
    {
        // Add hot-update notification registrations here, e.g.:
        // NotificationConst.Register("NEW_NOTIFICATION");
    }
}
