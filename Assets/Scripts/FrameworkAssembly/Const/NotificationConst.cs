using System.Collections.Generic;

/// <summary>
/// PureMVC notification name registry.
/// Base constants defined here; hot-update notifications registered at startup
/// via HotUpdateNotificationConst.RegisterTo().
/// 
/// Changed from const to static so hot-update registrations can be merged.
/// Usage: SendNotification(NotificationConst.STARTUP) — same as before.
/// </summary>
public static class NotificationConst
{
    private static readonly Dictionary<string, string> registry = new Dictionary<string, string>();

    // --- Startup ---
    public static string STARTUP => Get("STARTUP");
    public static string UPDATE_VIEW => Get("UPDATE_VIEW");

    // --- Login ---
    public static string LOGIN => Get("LOGIN");
    public static string LOGIN_SUCCESS => Get("LOGIN_SUCCESS");
    public static string LOGIN_FAILED => Get("LOGIN_FAILED");
    public static string LOGOUT => Get("LOGOUT");

    // --- Register ---
    public static string REGISTER => Get("REGISTER");
    public static string REGISTER_SUCCESS => Get("REGISTER_SUCCESS");
    public static string REGISTER_FAILED => Get("REGISTER_FAILED");

    // --- Create Player ---
    public static string CREATE_PLAYER => Get("CREATE_PLAYER");
    public static string CREATE_PLAYER_SUCCESS => Get("CREATE_PLAYER_SUCCESS");
    public static string CREATE_PLAYER_FAILED => Get("CREATE_PLAYER_FAILED");

    // --- User ---
    public static string UPDATE_USER_INFO => Get("UPDATE_USER_INFO");
    public static string LEVEL_UP => Get("LEVEL_UP");
    public static string ADD_EXP => Get("ADD_EXP");
    public static string ADD_GOLD => Get("ADD_GOLD");
    public static string ADD_DIAMOND => Get("ADD_DIAMOND");

    // --- Bag ---
    public static string UPDATE_BAG => Get("UPDATE_BAG");
    public static string USE_ITEM => Get("USE_ITEM");
    public static string ADD_ITEM => Get("ADD_ITEM");
    public static string RECYCLE_ITEM => Get("RECYCLE_ITEM");

    // --- Task ---
    public static string UPDATE_TASK => Get("UPDATE_TASK");
    public static string TASK_COMPLETE => Get("TASK_COMPLETE");
    public static string TASK_REWARD => Get("TASK_REWARD");

    // --- Achievement ---
    public static string UPDATE_ACHIEVEMENT => Get("UPDATE_ACHIEVEMENT");
    public static string ACHIEVEMENT_UNLOCK => Get("ACHIEVEMENT_UNLOCK");

    // --- Mail ---
    public static string UPDATE_MAIL => Get("UPDATE_MAIL");
    public static string MAIL_REWARD => Get("MAIL_REWARD");
    public static string MAIL_READ_ALL => Get("MAIL_READ_ALL");

    // --- Friend ---
    public static string UPDATE_FRIEND => Get("UPDATE_FRIEND");
    public static string ADD_FRIEND => Get("ADD_FRIEND");
    public static string DEL_FRIEND => Get("DEL_FRIEND");

    // --- Rank ---
    public static string UPDATE_RANK => Get("UPDATE_RANK");

    // --- Shop ---
    public static string UPDATE_SHOP => Get("UPDATE_SHOP");
    public static string BUY_ITEM => Get("BUY_ITEM");

    // --- Pet ---
    public static string UPDATE_PET => Get("UPDATE_PET");
    public static string PET_LEVEL_UP => Get("PET_LEVEL_UP");

    // --- Skill ---
    public static string USE_SKILL => Get("USE_SKILL");
    public static string UPDATE_SKILL_CD => Get("UPDATE_SKILL_CD");

    // --- Battle ---
    public static string ATTACK => Get("ATTACK");
    public static string HURT => Get("HURT");
    public static string SPAWN_FLOAT_TEXT => Get("SPAWN_FLOAT_TEXT");
    public static string UPDATE_FLOAT_TEXT => Get("UPDATE_FLOAT_TEXT");

    // --- Sign ---
    public static string UPDATE_SIGN => Get("UPDATE_SIGN");
    public static string SIGN_DAY => Get("SIGN_DAY");

    // --- Spin ---
    public static string SPIN_WHEEL => Get("SPIN_WHEEL");
    public static string SPIN_RESULT => Get("SPIN_RESULT");

    // --- Craft ---
    public static string CRAFT_ITEM => Get("CRAFT_ITEM");

    // --- Fashion ---
    public static string CHANGE_FASHION => Get("CHANGE_FASHION");

    // --- Setting ---
    public static string UPDATE_SETTING => Get("UPDATE_SETTING");

    // --- Dialog ---
    public static string NEXT_DIALOG => Get("NEXT_DIALOG");

    // --- UI ---
    public static string SHOW_TIP => Get("SHOW_TIP");
    public static string OPEN_PANEL => Get("OPEN_PANEL");
    public static string CLOSE_PANEL => Get("CLOSE_PANEL");

    // --- Hot Update ---
    public static string HOT_UPDATE_CHECK => Get("HOT_UPDATE_CHECK");
    public static string HOT_UPDATE_PROGRESS => Get("HOT_UPDATE_PROGRESS");
    public static string HOT_UPDATE_SUCCESS => Get("HOT_UPDATE_SUCCESS");
    public static string HOT_UPDATE_FAILED => Get("HOT_UPDATE_FAILED");
    public static string HOT_UPDATE_STATE_CHANGED => Get("HOT_UPDATE_STATE_CHANGED");
    public static string HOT_UPDATE_NEED_RESTART => Get("HOT_UPDATE_NEED_RESTART");
    public static string HOT_UPDATE_AVAILABLE => Get("HOT_UPDATE_AVAILABLE");

    // --- Friend ---
    public static string FRIEND_SEARCH_RESULT     => Get("FRIEND_SEARCH_RESULT");
    public static string FRIEND_APPLY_RESULT      => Get("FRIEND_APPLY_RESULT");
    public static string FRIEND_APPLY_LIST_UPDATED => Get("FRIEND_APPLY_LIST_UPDATED");
    public static string FRIEND_REPLY_RESULT      => Get("FRIEND_REPLY_RESULT");
    public static string FRIEND_LIST_UPDATED      => Get("FRIEND_LIST_UPDATED");
    public static string FRIEND_DELETE_RESULT     => Get("FRIEND_DELETE_RESULT");
    public static string FRIEND_REMARK_RESULT     => Get("FRIEND_REMARK_RESULT");
    public static string FRIEND_BLOCK_RESULT      => Get("FRIEND_BLOCK_RESULT");
    public static string FRIEND_BLOCK_LIST_UPDATED => Get("FRIEND_BLOCK_LIST_UPDATED");
    public static string FRIEND_ONLINE_UPDATED    => Get("FRIEND_ONLINE_UPDATED");

    // --- Mail ---
    public static string MAIL_LIST_UPDATED => Get("MAIL_LIST_UPDATED");
    public static string MAIL_NEW_NOTIFY   => Get("MAIL_NEW_NOTIFY");

    // --- Announce ---
    public static string ANNOUNCE_LIST_UPDATED => Get("ANNOUNCE_LIST_UPDATED");
    public static string ANNOUNCE_NOTIFY       => Get("ANNOUNCE_NOTIFY");

    // --- Sign In ---
    public static string SIGNIN_INFO_UPDATED => Get("SIGNIN_INFO_UPDATED");
    public static string SIGNIN_DO_RESULT    => Get("SIGNIN_DO_RESULT");

    // --- Bag ---
    public static string BAG_LIST_UPDATED => Get("BAG_LIST_UPDATED");
    public static string BAG_ITEM_CHANGED => Get("BAG_ITEM_CHANGED");

    // --- Save ---
    public static string SAVE_DATA => Get("SAVE_DATA");
    public static string LOAD_DATA => Get("LOAD_DATA");

    // --- Game ---
    public static string PAUSE_GAME => Get("PAUSE_GAME");
    public static string RESUME_GAME => Get("RESUME_GAME");

    #region Registration

    static NotificationConst()
    {
        // Pre-register all base notification names (self-registration)
        Register("STARTUP"); Register("UPDATE_VIEW");
        Register("LOGIN"); Register("LOGIN_SUCCESS"); Register("LOGIN_FAILED"); Register("LOGOUT");
        Register("REGISTER"); Register("REGISTER_SUCCESS"); Register("REGISTER_FAILED");
        Register("CREATE_PLAYER"); Register("CREATE_PLAYER_SUCCESS"); Register("CREATE_PLAYER_FAILED");
        Register("UPDATE_USER_INFO"); Register("LEVEL_UP"); Register("ADD_EXP"); Register("ADD_GOLD"); Register("ADD_DIAMOND");
        Register("UPDATE_BAG"); Register("USE_ITEM"); Register("ADD_ITEM"); Register("RECYCLE_ITEM");
        Register("UPDATE_TASK"); Register("TASK_COMPLETE"); Register("TASK_REWARD");
        Register("UPDATE_ACHIEVEMENT"); Register("ACHIEVEMENT_UNLOCK");
        Register("UPDATE_MAIL"); Register("MAIL_REWARD"); Register("MAIL_READ_ALL");
        Register("UPDATE_FRIEND"); Register("ADD_FRIEND"); Register("DEL_FRIEND");
        Register("UPDATE_RANK");
        Register("UPDATE_SHOP"); Register("BUY_ITEM");
        Register("UPDATE_PET"); Register("PET_LEVEL_UP");
        Register("USE_SKILL"); Register("UPDATE_SKILL_CD");
        Register("ATTACK"); Register("HURT"); Register("SPAWN_FLOAT_TEXT"); Register("UPDATE_FLOAT_TEXT");
        Register("UPDATE_SIGN"); Register("SIGN_DAY");
        Register("SPIN_WHEEL"); Register("SPIN_RESULT");
        Register("CRAFT_ITEM");
        Register("CHANGE_FASHION");
        Register("UPDATE_SETTING");
        Register("NEXT_DIALOG");
        Register("SHOW_TIP"); Register("OPEN_PANEL"); Register("CLOSE_PANEL");
        Register("HOT_UPDATE_CHECK"); Register("HOT_UPDATE_PROGRESS"); Register("HOT_UPDATE_SUCCESS"); Register("HOT_UPDATE_FAILED"); Register("HOT_UPDATE_STATE_CHANGED"); Register("HOT_UPDATE_NEED_RESTART");
        Register("HOT_UPDATE_AVAILABLE");
        Register("FRIEND_SEARCH_RESULT"); Register("FRIEND_APPLY_RESULT"); Register("FRIEND_APPLY_LIST_UPDATED");
        Register("FRIEND_REPLY_RESULT"); Register("FRIEND_LIST_UPDATED"); Register("FRIEND_DELETE_RESULT");
        Register("FRIEND_REMARK_RESULT"); Register("FRIEND_BLOCK_RESULT"); Register("FRIEND_BLOCK_LIST_UPDATED");
        Register("FRIEND_ONLINE_UPDATED");
        Register("MAIL_LIST_UPDATED"); Register("MAIL_NEW_NOTIFY");
        Register("ANNOUNCE_LIST_UPDATED"); Register("ANNOUNCE_NOTIFY");
        Register("SIGNIN_INFO_UPDATED"); Register("SIGNIN_DO_RESULT");
        Register("BAG_LIST_UPDATED"); Register("BAG_ITEM_CHANGED");
        Register("SAVE_DATA"); Register("LOAD_DATA");
        Register("PAUSE_GAME"); Register("RESUME_GAME");
    }

    /// <summary>
    /// Register a notification name. Called by HotUpdateNotificationConst at startup.
    /// Idempotent — duplicate registrations are ignored.
    /// </summary>
    public static void Register(string name)
    {
        if (!registry.ContainsKey(name))
            registry[name] = name;
    }

    /// <summary>
    /// Get a notification name by key. Returns the key itself if not found.
    /// </summary>
    public static string Get(string key)
    {
        if (registry.TryGetValue(key, out string value))
            return value;
        return key;
    }

    #endregion
}
