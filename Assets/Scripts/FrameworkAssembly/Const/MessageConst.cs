using System.Collections.Generic;

/// <summary>
/// Message ID constants for TCP protocol.
/// Range conventions:
///   1000-1999  System / Connection
///   2000-2999  Account / Login
///   3000-3999  Heartbeat
/// 
/// Changed from const to static int so hot-update IDs can be merged.
/// </summary>
public static class MessageConst
{
    private static readonly Dictionary<string, int> registry = new Dictionary<string, int>();

    // --- System ---
    public static int CONNECT_S2C => Get("CONNECT_S2C");

    // --- Login ---
    public static int LOGIN_C2S => Get("LOGIN_C2S");
    public static int LOGIN_S2C => Get("LOGIN_S2C");

    // --- Register ---
    public static int REGISTER_C2S => Get("REGISTER_C2S");
    public static int REGISTER_S2C => Get("REGISTER_S2C");

    // --- Create Player ---
    public static int CREATE_PLAYER_C2S => Get("CREATE_PLAYER_C2S");
    public static int CREATE_PLAYER_S2C => Get("CREATE_PLAYER_S2C");

    // --- Heartbeat ---
    public static int HEARTBEAT_C2S => Get("HEARTBEAT_C2S");
    public static int HEARTBEAT_S2C => Get("HEARTBEAT_S2C");

    // --- Friend ---
    public static int FRIEND_SEARCH_C2S   => Get("FRIEND_SEARCH_C2S");
    public static int FRIEND_SEARCH_S2C   => Get("FRIEND_SEARCH_S2C");
    public static int FRIEND_APPLY_C2S    => Get("FRIEND_APPLY_C2S");
    public static int FRIEND_APPLY_S2C    => Get("FRIEND_APPLY_S2C");
    public static int FRIEND_APPLY_LIST_C2S => Get("FRIEND_APPLY_LIST_C2S");
    public static int FRIEND_APPLY_LIST_S2C => Get("FRIEND_APPLY_LIST_S2C");
    public static int FRIEND_REPLY_C2S    => Get("FRIEND_REPLY_C2S");
    public static int FRIEND_REPLY_S2C    => Get("FRIEND_REPLY_S2C");
    public static int FRIEND_LIST_C2S     => Get("FRIEND_LIST_C2S");
    public static int FRIEND_LIST_S2C     => Get("FRIEND_LIST_S2C");
    public static int FRIEND_DELETE_C2S   => Get("FRIEND_DELETE_C2S");
    public static int FRIEND_DELETE_S2C   => Get("FRIEND_DELETE_S2C");
    public static int FRIEND_REMARK_C2S   => Get("FRIEND_REMARK_C2S");
    public static int FRIEND_REMARK_S2C   => Get("FRIEND_REMARK_S2C");
    public static int FRIEND_BLOCK_C2S    => Get("FRIEND_BLOCK_C2S");
    public static int FRIEND_BLOCK_S2C    => Get("FRIEND_BLOCK_S2C");
    public static int FRIEND_BLOCK_LIST_C2S => Get("FRIEND_BLOCK_LIST_C2S");
    public static int FRIEND_BLOCK_LIST_S2C => Get("FRIEND_BLOCK_LIST_S2C");
    public static int FRIEND_ONLINE_NOTIFY_S2C => Get("FRIEND_ONLINE_NOTIFY_S2C");

    // --- Mail ---
    public static int MAIL_LIST_C2S   => Get("MAIL_LIST_C2S");
    public static int MAIL_LIST_S2C   => Get("MAIL_LIST_S2C");
    public static int MAIL_READ_C2S   => Get("MAIL_READ_C2S");
    public static int MAIL_READ_S2C   => Get("MAIL_READ_S2C");
    public static int MAIL_CLAIM_C2S  => Get("MAIL_CLAIM_C2S");
    public static int MAIL_CLAIM_S2C  => Get("MAIL_CLAIM_S2C");
    public static int MAIL_DELETE_C2S => Get("MAIL_DELETE_C2S");
    public static int MAIL_DELETE_S2C => Get("MAIL_DELETE_S2C");
    public static int MAIL_NEW_NOTIFY_S2C => Get("MAIL_NEW_NOTIFY_S2C");

    // --- Announce ---
    public static int ANNOUNCE_LIST_C2S   => Get("ANNOUNCE_LIST_C2S");
    public static int ANNOUNCE_LIST_S2C   => Get("ANNOUNCE_LIST_S2C");
    public static int ANNOUNCE_NOTIFY_S2C => Get("ANNOUNCE_NOTIFY_S2C");

    // --- Sign In ---
    public static int SIGNIN_INFO_C2S   => Get("SIGNIN_INFO_C2S");
    public static int SIGNIN_INFO_S2C   => Get("SIGNIN_INFO_S2C");
    public static int SIGNIN_DO_C2S     => Get("SIGNIN_DO_C2S");
    public static int SIGNIN_DO_S2C     => Get("SIGNIN_DO_S2C");
    public static int SIGNIN_MAKEUP_C2S => Get("SIGNIN_MAKEUP_C2S");
    public static int SIGNIN_MAKEUP_S2C => Get("SIGNIN_MAKEUP_S2C");

    // --- Bag ---
    public static int BAG_LIST_C2S => Get("BAG_LIST_C2S");
    public static int BAG_LIST_S2C => Get("BAG_LIST_S2C");
    public static int BAG_USE_C2S  => Get("BAG_USE_C2S");
    public static int BAG_USE_S2C  => Get("BAG_USE_S2C");
    public static int BAG_SELL_C2S => Get("BAG_SELL_C2S");
    public static int BAG_SELL_S2C => Get("BAG_SELL_S2C");

    // --- Shop ---
    public static int SHOP_LIST_C2S => Get("SHOP_LIST_C2S");
    public static int SHOP_LIST_S2C => Get("SHOP_LIST_S2C");
    public static int SHOP_BUY_C2S  => Get("SHOP_BUY_C2S");
    public static int SHOP_BUY_S2C  => Get("SHOP_BUY_S2C");

    // --- Achievement ---
    public static int ACHIEVEMENT_LIST_C2S     => Get("ACHIEVEMENT_LIST_C2S");
    public static int ACHIEVEMENT_LIST_S2C     => Get("ACHIEVEMENT_LIST_S2C");
    public static int ACHIEVEMENT_PROGRESS_S2C => Get("ACHIEVEMENT_PROGRESS_S2C");
    public static int ACHIEVEMENT_UNLOCK_S2C   => Get("ACHIEVEMENT_UNLOCK_S2C");
    public static int ACHIEVEMENT_CLAIM_C2S    => Get("ACHIEVEMENT_CLAIM_C2S");
    public static int ACHIEVEMENT_CLAIM_S2C    => Get("ACHIEVEMENT_CLAIM_S2C");

    #region Registration

    static MessageConst()
    {
        Register("CONNECT_S2C", 1001);
        Register("LOGIN_C2S", 2001);
        Register("LOGIN_S2C", 2002);
        Register("REGISTER_C2S", 5001);
        Register("REGISTER_S2C", 5002);
        Register("CREATE_PLAYER_C2S", 6001);
        Register("CREATE_PLAYER_S2C", 6002);
        Register("HEARTBEAT_C2S", 3001);
        Register("HEARTBEAT_S2C", 3002);
        Register("FRIEND_SEARCH_C2S", 20001);
        Register("FRIEND_SEARCH_S2C", 20002);
        Register("FRIEND_APPLY_C2S", 20003);
        Register("FRIEND_APPLY_S2C", 20004);
        Register("FRIEND_APPLY_LIST_C2S", 20005);
        Register("FRIEND_APPLY_LIST_S2C", 20006);
        Register("FRIEND_REPLY_C2S", 20007);
        Register("FRIEND_REPLY_S2C", 20008);
        Register("FRIEND_LIST_C2S", 20009);
        Register("FRIEND_LIST_S2C", 20010);
        Register("FRIEND_DELETE_C2S", 20011);
        Register("FRIEND_DELETE_S2C", 20012);
        Register("FRIEND_REMARK_C2S", 20013);
        Register("FRIEND_REMARK_S2C", 20014);
        Register("FRIEND_BLOCK_C2S", 20015);
        Register("FRIEND_BLOCK_S2C", 20016);
        Register("FRIEND_BLOCK_LIST_C2S", 20017);
        Register("FRIEND_BLOCK_LIST_S2C", 20018);
        Register("FRIEND_ONLINE_NOTIFY_S2C", 20019);
        Register("MAIL_LIST_C2S", 21001);
        Register("MAIL_LIST_S2C", 21002);
        Register("MAIL_READ_C2S", 21003);
        Register("MAIL_READ_S2C", 21004);
        Register("MAIL_CLAIM_C2S", 21005);
        Register("MAIL_CLAIM_S2C", 21006);
        Register("MAIL_DELETE_C2S", 21007);
        Register("MAIL_DELETE_S2C", 21008);
        Register("MAIL_NEW_NOTIFY_S2C", 21009);
        Register("ANNOUNCE_LIST_C2S", 22001);
        Register("ANNOUNCE_LIST_S2C", 22002);
        Register("ANNOUNCE_NOTIFY_S2C", 22003);
        Register("SIGNIN_INFO_C2S", 23001);
        Register("SIGNIN_INFO_S2C", 23002);
        Register("SIGNIN_DO_C2S", 23003);
        Register("SIGNIN_DO_S2C", 23004);
        Register("SIGNIN_MAKEUP_C2S", 23005);
        Register("SIGNIN_MAKEUP_S2C", 23006);
        Register("BAG_LIST_C2S", 24001);
        Register("BAG_LIST_S2C", 24002);
        Register("BAG_USE_C2S", 24003);
        Register("BAG_USE_S2C", 24004);
        Register("BAG_SELL_C2S", 24005);
        Register("BAG_SELL_S2C", 24006);
        Register("SHOP_LIST_C2S", 25001);
        Register("SHOP_LIST_S2C", 25002);
        Register("SHOP_BUY_C2S", 25003);
        Register("SHOP_BUY_S2C", 25004);
        Register("ACHIEVEMENT_LIST_C2S", 26001);
        Register("ACHIEVEMENT_LIST_S2C", 26002);
        Register("ACHIEVEMENT_PROGRESS_S2C", 26003);
        Register("ACHIEVEMENT_UNLOCK_S2C", 26004);
        Register("ACHIEVEMENT_CLAIM_C2S", 26005);
        Register("ACHIEVEMENT_CLAIM_S2C", 26006);
    }

    /// <summary>
    /// Register a message ID. Called by HotUpdateMessageConst at startup.
    /// </summary>
    public static void Register(string name, int id)
    {
        if (!registry.ContainsKey(name))
            registry[name] = id;
    }

    /// <summary>
    /// Get message ID by name. Returns 0 if not found.
    /// </summary>
    public static int Get(string key)
    {
        if (registry.TryGetValue(key, out int value))
            return value;
        Log.w($"MessageConst: '{key}' not registered — returning 0", "MessageConst");
        return 0;
    }

    #endregion
}
