/// <summary>
/// Hot-update MessageConst registrations.
/// Called by GameMain.InitModule() at startup to merge into FrameworkAssembly's MessageConst.
/// 
/// Add new message IDs for hot-update features here.
/// Use HotUpdateMessageConst.XXX at compile time; MessageConst.XXX at runtime (after RegisterTo).
/// </summary>
public static class HotUpdateMessageConst
{
    // Chat (reserved)
    // public const int CHAT_C2S = 7001;
    // public const int CHAT_S2C = 7002;

    // Player Extension
    public const int PLAYER_EXT_C2S = 8001;
    public const int PLAYER_EXT_S2C = 8002;

    /// <summary>
    /// Register all hot-update message IDs into FrameworkAssembly's MessageConst.
    /// Called via reflection from GameMain.InitModule().
    /// </summary>
    public static void RegisterTo()
    {
        // Chat
        // MessageConst.Register("CHAT_C2S", CHAT_C2S);
        // MessageConst.Register("CHAT_S2C", CHAT_S2C);

        // Player Extension
        MessageConst.Register("PLAYER_EXT_C2S", PLAYER_EXT_C2S);
        MessageConst.Register("PLAYER_EXT_S2C", PLAYER_EXT_S2C);
    }
}
