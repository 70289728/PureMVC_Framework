using NUnit.Framework;

/// <summary>
/// Verify all 70+ NotificationConst keys are registered and retrievable.
/// Catches typos where Register("XXX") doesn't match the property name.
/// </summary>
public class NotificationConstTests
{
    [Test]
    public void AllRegisteredKeys_ReturnNonEmpty()
    {
        // Startup
        Assert.AreEqual("STARTUP",             NotificationConst.STARTUP);
        Assert.AreEqual("UPDATE_VIEW",         NotificationConst.UPDATE_VIEW);

        // Login
        Assert.AreEqual("LOGIN",               NotificationConst.LOGIN);
        Assert.AreEqual("LOGIN_SUCCESS",       NotificationConst.LOGIN_SUCCESS);
        Assert.AreEqual("LOGIN_FAILED",        NotificationConst.LOGIN_FAILED);
        Assert.AreEqual("LOGOUT",              NotificationConst.LOGOUT);

        // Register
        Assert.AreEqual("REGISTER",            NotificationConst.REGISTER);
        Assert.AreEqual("REGISTER_SUCCESS",    NotificationConst.REGISTER_SUCCESS);
        Assert.AreEqual("REGISTER_FAILED",     NotificationConst.REGISTER_FAILED);

        // Create Player
        Assert.AreEqual("CREATE_PLAYER",       NotificationConst.CREATE_PLAYER);
        Assert.AreEqual("CREATE_PLAYER_SUCCESS", NotificationConst.CREATE_PLAYER_SUCCESS);
        Assert.AreEqual("CREATE_PLAYER_FAILED",  NotificationConst.CREATE_PLAYER_FAILED);

        // User
        Assert.AreEqual("UPDATE_USER_INFO",    NotificationConst.UPDATE_USER_INFO);
        Assert.AreEqual("LEVEL_UP",            NotificationConst.LEVEL_UP);
        Assert.AreEqual("ADD_EXP",             NotificationConst.ADD_EXP);
        Assert.AreEqual("ADD_GOLD",            NotificationConst.ADD_GOLD);
        Assert.AreEqual("ADD_DIAMOND",         NotificationConst.ADD_DIAMOND);

        // Bag
        Assert.AreEqual("UPDATE_BAG",          NotificationConst.UPDATE_BAG);
        Assert.AreEqual("USE_ITEM",            NotificationConst.USE_ITEM);
        Assert.AreEqual("ADD_ITEM",            NotificationConst.ADD_ITEM);
        Assert.AreEqual("RECYCLE_ITEM",        NotificationConst.RECYCLE_ITEM);

        // Task
        Assert.AreEqual("UPDATE_TASK",         NotificationConst.UPDATE_TASK);
        Assert.AreEqual("TASK_COMPLETE",       NotificationConst.TASK_COMPLETE);
        Assert.AreEqual("TASK_REWARD",         NotificationConst.TASK_REWARD);

        // Achievement
        Assert.AreEqual("UPDATE_ACHIEVEMENT",  NotificationConst.UPDATE_ACHIEVEMENT);
        Assert.AreEqual("ACHIEVEMENT_UNLOCK",  NotificationConst.ACHIEVEMENT_UNLOCK);

        // Mail
        Assert.AreEqual("UPDATE_MAIL",         NotificationConst.UPDATE_MAIL);
        Assert.AreEqual("MAIL_REWARD",         NotificationConst.MAIL_REWARD);
        Assert.AreEqual("MAIL_READ_ALL",       NotificationConst.MAIL_READ_ALL);

        // Friend
        Assert.AreEqual("UPDATE_FRIEND",       NotificationConst.UPDATE_FRIEND);
        Assert.AreEqual("ADD_FRIEND",          NotificationConst.ADD_FRIEND);
        Assert.AreEqual("DEL_FRIEND",          NotificationConst.DEL_FRIEND);

        // Rank
        Assert.AreEqual("UPDATE_RANK",         NotificationConst.UPDATE_RANK);

        // Shop
        Assert.AreEqual("UPDATE_SHOP",         NotificationConst.UPDATE_SHOP);
        Assert.AreEqual("BUY_ITEM",            NotificationConst.BUY_ITEM);

        // Pet
        Assert.AreEqual("UPDATE_PET",          NotificationConst.UPDATE_PET);
        Assert.AreEqual("PET_LEVEL_UP",        NotificationConst.PET_LEVEL_UP);

        // Skill
        Assert.AreEqual("USE_SKILL",           NotificationConst.USE_SKILL);
        Assert.AreEqual("UPDATE_SKILL_CD",     NotificationConst.UPDATE_SKILL_CD);

        // Battle
        Assert.AreEqual("ATTACK",              NotificationConst.ATTACK);
        Assert.AreEqual("HURT",                NotificationConst.HURT);
        Assert.AreEqual("SPAWN_FLOAT_TEXT",    NotificationConst.SPAWN_FLOAT_TEXT);
        Assert.AreEqual("UPDATE_FLOAT_TEXT",   NotificationConst.UPDATE_FLOAT_TEXT);

        // Sign
        Assert.AreEqual("UPDATE_SIGN",         NotificationConst.UPDATE_SIGN);
        Assert.AreEqual("SIGN_DAY",            NotificationConst.SIGN_DAY);

        // Spin
        Assert.AreEqual("SPIN_WHEEL",          NotificationConst.SPIN_WHEEL);
        Assert.AreEqual("SPIN_RESULT",         NotificationConst.SPIN_RESULT);

        // Craft
        Assert.AreEqual("CRAFT_ITEM",          NotificationConst.CRAFT_ITEM);

        // Fashion
        Assert.AreEqual("CHANGE_FASHION",      NotificationConst.CHANGE_FASHION);

        // Setting
        Assert.AreEqual("UPDATE_SETTING",      NotificationConst.UPDATE_SETTING);

        // Dialog
        Assert.AreEqual("NEXT_DIALOG",         NotificationConst.NEXT_DIALOG);

        // UI
        Assert.AreEqual("SHOW_TIP",            NotificationConst.SHOW_TIP);
        Assert.AreEqual("SHOW_DIALOG",         NotificationConst.SHOW_DIALOG);
        Assert.AreEqual("CLOSE_DIALOG",        NotificationConst.CLOSE_DIALOG);
        Assert.AreEqual("OPEN_PANEL",          NotificationConst.OPEN_PANEL);
        Assert.AreEqual("CLOSE_PANEL",         NotificationConst.CLOSE_PANEL);

        // Hot Update
        Assert.AreEqual("HOT_UPDATE_CHECK",    NotificationConst.HOT_UPDATE_CHECK);
        Assert.AreEqual("HOT_UPDATE_PROGRESS", NotificationConst.HOT_UPDATE_PROGRESS);
        Assert.AreEqual("HOT_UPDATE_SUCCESS",  NotificationConst.HOT_UPDATE_SUCCESS);
        Assert.AreEqual("HOT_UPDATE_FAILED",   NotificationConst.HOT_UPDATE_FAILED);
        Assert.AreEqual("HOT_UPDATE_STATE_CHANGED", NotificationConst.HOT_UPDATE_STATE_CHANGED);
        Assert.AreEqual("HOT_UPDATE_NEED_RESTART",  NotificationConst.HOT_UPDATE_NEED_RESTART);
        Assert.AreEqual("HOT_UPDATE_AVAILABLE",     NotificationConst.HOT_UPDATE_AVAILABLE);

        // Friend (extended)
        Assert.AreEqual("FRIEND_SEARCH_RESULT",     NotificationConst.FRIEND_SEARCH_RESULT);
        Assert.AreEqual("FRIEND_APPLY_RESULT",      NotificationConst.FRIEND_APPLY_RESULT);
        Assert.AreEqual("FRIEND_APPLY_LIST_UPDATED",NotificationConst.FRIEND_APPLY_LIST_UPDATED);
        Assert.AreEqual("FRIEND_REPLY_RESULT",      NotificationConst.FRIEND_REPLY_RESULT);
        Assert.AreEqual("FRIEND_LIST_UPDATED",      NotificationConst.FRIEND_LIST_UPDATED);
        Assert.AreEqual("FRIEND_DELETE_RESULT",     NotificationConst.FRIEND_DELETE_RESULT);
        Assert.AreEqual("FRIEND_REMARK_RESULT",     NotificationConst.FRIEND_REMARK_RESULT);
        Assert.AreEqual("FRIEND_BLOCK_RESULT",      NotificationConst.FRIEND_BLOCK_RESULT);
        Assert.AreEqual("FRIEND_BLOCK_LIST_UPDATED",NotificationConst.FRIEND_BLOCK_LIST_UPDATED);
        Assert.AreEqual("FRIEND_ONLINE_UPDATED",    NotificationConst.FRIEND_ONLINE_UPDATED);

        // Mail (extended)
        Assert.AreEqual("MAIL_LIST_UPDATED",        NotificationConst.MAIL_LIST_UPDATED);
        Assert.AreEqual("MAIL_NEW_NOTIFY",          NotificationConst.MAIL_NEW_NOTIFY);

        // Announce
        Assert.AreEqual("ANNOUNCE_LIST_UPDATED",    NotificationConst.ANNOUNCE_LIST_UPDATED);
        Assert.AreEqual("ANNOUNCE_NOTIFY",          NotificationConst.ANNOUNCE_NOTIFY);

        // Sign In
        Assert.AreEqual("SIGNIN_INFO_UPDATED",      NotificationConst.SIGNIN_INFO_UPDATED);
        Assert.AreEqual("SIGNIN_DO_RESULT",         NotificationConst.SIGNIN_DO_RESULT);

        // Bag (extended)
        Assert.AreEqual("BAG_LIST_UPDATED",         NotificationConst.BAG_LIST_UPDATED);
        Assert.AreEqual("BAG_ITEM_CHANGED",         NotificationConst.BAG_ITEM_CHANGED);

        // Save
        Assert.AreEqual("SAVE_DATA",                NotificationConst.SAVE_DATA);
        Assert.AreEqual("LOAD_DATA",                NotificationConst.LOAD_DATA);

        // Game
        Assert.AreEqual("PAUSE_GAME",               NotificationConst.PAUSE_GAME);
        Assert.AreEqual("RESUME_GAME",              NotificationConst.RESUME_GAME);

        // System
        Assert.AreEqual("SYS_ERROR",                NotificationConst.SYS_ERROR);
    }
}
