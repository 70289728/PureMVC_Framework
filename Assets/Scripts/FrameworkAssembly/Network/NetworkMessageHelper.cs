/// <summary>
/// Helper class for building and sending typed network messages.
/// Automatically fills MessageHeader.MessageId before sending.
/// Usage: NetworkMessageHelper.SendLogin(accountId, password);
/// </summary>
public static class NetworkMessageHelper
{
    private static NetworkManager Net => NetworkManager.Instance;

    // -------------------------------------------------------------------------
    // Login
    // -------------------------------------------------------------------------

    /// <summary>
    /// Send a login request to the server.
    /// </summary>
    public static void SendLogin(int accountId, string password)
    {
        var msg = new LoginMessageC2S
        {
            AccountId = accountId,
            Password  = AesHelper.EncryptString(password)
        };
        Net.Send(MessageConst.LOGIN_C2S, msg);
    }

    // -------------------------------------------------------------------------
    // Register
    // -------------------------------------------------------------------------

    /// <summary>
    /// Send a register request to the server.
    /// </summary>
    public static void SendRegister(int accountId, string password)
    {
        var msg = new RegisterC2S
        {
            AccountId = accountId,
            Password  = AesHelper.EncryptString(password)
        };
        Net.Send(MessageConst.REGISTER_C2S, msg);
    }

    // -------------------------------------------------------------------------
    // Create Player
    // -------------------------------------------------------------------------

    /// <summary>
    /// Send a create player request to the server.
    /// </summary>
    public static void SendCreatePlayer(string playerName, int gender, int job)
    {
        var msg = new CreatePlayerC2S
        {
            PlayerName = playerName,
            Gender     = gender,
            Job        = job
        };
        Net.Send(MessageConst.CREATE_PLAYER_C2S, msg);
    }

    // -------------------------------------------------------------------------
    // Heartbeat
    // -------------------------------------------------------------------------

    // -------------------------------------------------------------------------
    // Response parsers (decode body bytes → typed proto)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Parse a ConnectS2C response from raw body bytes.
    /// Register: NetworkManager.Instance.Dispatcher.Register(MessageConst.CONNECT_S2C, OnConnect);
    /// </summary>
    public static ConnectS2C ParseConnectS2C(byte[] body)
        => ConnectS2C.Parser.ParseFrom(body);

    /// <summary>
    /// Parse a LoginMessageS2C response from raw body bytes.
    /// Register: NetworkManager.Instance.Dispatcher.Register(MessageConst.LOGIN_S2C, OnLoginResponse);
    /// </summary>
    public static LoginMessageS2C ParseLoginS2C(byte[] body)
        => LoginMessageS2C.Parser.ParseFrom(body);

    /// <summary>
    /// Parse a HeartbeatS2C response from raw body bytes.
    /// </summary>
    public static HeartbeatS2C ParseHeartbeatS2C(byte[] body)
        => HeartbeatS2C.Parser.ParseFrom(body);

    /// <summary>
    /// Parse a RegisterS2C response from raw body bytes.
    /// </summary>
    public static RegisterS2C ParseRegisterS2C(byte[] body)
        => RegisterS2C.Parser.ParseFrom(body);

    /// <summary>
    /// Parse a CreatePlayerS2C response from raw body bytes.
    /// </summary>
    public static CreatePlayerS2C ParseCreatePlayerS2C(byte[] body)
        => CreatePlayerS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Friend — Send
    // -------------------------------------------------------------------------

    public static void SendFriendSearch(string keyword)
        => Net.Send(MessageConst.FRIEND_SEARCH_C2S, new FriendSearchC2S { Keyword = keyword });

    public static void SendFriendApply(long targetPlayerId)
        => Net.Send(MessageConst.FRIEND_APPLY_C2S, new FriendApplyC2S { TargetPlayerId = targetPlayerId });

    public static void SendFriendApplyList()
        => Net.Send(MessageConst.FRIEND_APPLY_LIST_C2S, new FriendApplyListC2S());

    public static void SendFriendReply(long fromPlayerId, bool agree)
        => Net.Send(MessageConst.FRIEND_REPLY_C2S, new FriendReplyC2S { FromPlayerId = fromPlayerId, Agree = agree });

    public static void SendFriendList()
        => Net.Send(MessageConst.FRIEND_LIST_C2S, new FriendListC2S());

    public static void SendFriendDelete(long friendPlayerId)
        => Net.Send(MessageConst.FRIEND_DELETE_C2S, new FriendDeleteC2S { FriendPlayerId = friendPlayerId });

    public static void SendFriendRemark(long friendPlayerId, string remark)
        => Net.Send(MessageConst.FRIEND_REMARK_C2S, new FriendRemarkC2S { FriendPlayerId = friendPlayerId, Remark = remark });

    public static void SendFriendBlock(long playerId, bool isBlock)
        => Net.Send(MessageConst.FRIEND_BLOCK_C2S, new FriendBlockC2S { PlayerId = playerId, IsBlock = isBlock });

    public static void SendFriendBlockList()
        => Net.Send(MessageConst.FRIEND_BLOCK_LIST_C2S, new FriendBlockListC2S());

    // -------------------------------------------------------------------------
    // Friend — Parse
    // -------------------------------------------------------------------------

    public static FriendSearchS2C ParseFriendSearchS2C(byte[] body) => FriendSearchS2C.Parser.ParseFrom(body);
    public static FriendApplyS2C ParseFriendApplyS2C(byte[] body) => FriendApplyS2C.Parser.ParseFrom(body);
    public static FriendApplyListS2C ParseFriendApplyListS2C(byte[] body) => FriendApplyListS2C.Parser.ParseFrom(body);
    public static FriendReplyS2C ParseFriendReplyS2C(byte[] body) => FriendReplyS2C.Parser.ParseFrom(body);
    public static FriendListS2C ParseFriendListS2C(byte[] body) => FriendListS2C.Parser.ParseFrom(body);
    public static FriendDeleteS2C ParseFriendDeleteS2C(byte[] body) => FriendDeleteS2C.Parser.ParseFrom(body);
    public static FriendRemarkS2C ParseFriendRemarkS2C(byte[] body) => FriendRemarkS2C.Parser.ParseFrom(body);
    public static FriendBlockS2C ParseFriendBlockS2C(byte[] body) => FriendBlockS2C.Parser.ParseFrom(body);
    public static FriendBlockListS2C ParseFriendBlockListS2C(byte[] body) => FriendBlockListS2C.Parser.ParseFrom(body);
    public static FriendOnlineNotifyS2C ParseFriendOnlineNotifyS2C(byte[] body) => FriendOnlineNotifyS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Mail
    // -------------------------------------------------------------------------

    public static void SendMailList() => Net.Send(MessageConst.MAIL_LIST_C2S, new MailListC2S());
    public static void SendMailRead(long mailId) => Net.Send(MessageConst.MAIL_READ_C2S, new MailReadC2S { MailId = mailId });
    public static void SendMailClaim(long mailId) => Net.Send(MessageConst.MAIL_CLAIM_C2S, new MailClaimC2S { MailId = mailId });
    public static void SendMailDelete(System.Collections.Generic.List<long> mailIds)
    {
        var msg = new MailDeleteC2S();
        msg.MailIds.AddRange(mailIds);
        Net.Send(MessageConst.MAIL_DELETE_C2S, msg);
    }

    public static MailListS2C ParseMailListS2C(byte[] body) => MailListS2C.Parser.ParseFrom(body);
    public static MailReadS2C ParseMailReadS2C(byte[] body) => MailReadS2C.Parser.ParseFrom(body);
    public static MailClaimS2C ParseMailClaimS2C(byte[] body) => MailClaimS2C.Parser.ParseFrom(body);
    public static MailDeleteS2C ParseMailDeleteS2C(byte[] body) => MailDeleteS2C.Parser.ParseFrom(body);
    public static MailNewNotifyS2C ParseMailNewNotifyS2C(byte[] body) => MailNewNotifyS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Announce
    // -------------------------------------------------------------------------

    public static void SendAnnounceList() => Net.Send(MessageConst.ANNOUNCE_LIST_C2S, new AnnounceListC2S());

    public static AnnounceListS2C ParseAnnounceListS2C(byte[] body) => AnnounceListS2C.Parser.ParseFrom(body);
    public static AnnounceNotifyS2C ParseAnnounceNotifyS2C(byte[] body) => AnnounceNotifyS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Sign In
    // -------------------------------------------------------------------------

    public static void SendSignInInfo() => Net.Send(MessageConst.SIGNIN_INFO_C2S, new SignInInfoC2S());
    public static void SendSignInDo() => Net.Send(MessageConst.SIGNIN_DO_C2S, new SignInDoC2S());
    public static void SendSignInMakeUp() => Net.Send(MessageConst.SIGNIN_MAKEUP_C2S, new SignInMakeUpC2S());

    public static SignInInfoS2C ParseSignInInfoS2C(byte[] body) => SignInInfoS2C.Parser.ParseFrom(body);
    public static SignInDoS2C ParseSignInDoS2C(byte[] body) => SignInDoS2C.Parser.ParseFrom(body);
    public static SignInMakeUpS2C ParseSignInMakeUpS2C(byte[] body) => SignInMakeUpS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Bag
    // -------------------------------------------------------------------------

    public static void SendBagList() => Net.Send(MessageConst.BAG_LIST_C2S, new BagListC2S());
    public static void SendBagUse(int itemId, int count) => Net.Send(MessageConst.BAG_USE_C2S, new BagUseC2S { ItemId = itemId, Count = count });
    public static void SendBagSell(int itemId, int count) => Net.Send(MessageConst.BAG_SELL_C2S, new BagSellC2S { ItemId = itemId, Count = count });

    public static BagListS2C ParseBagListS2C(byte[] body) => BagListS2C.Parser.ParseFrom(body);
    public static BagUseS2C ParseBagUseS2C(byte[] body) => BagUseS2C.Parser.ParseFrom(body);
    public static BagSellS2C ParseBagSellS2C(byte[] body) => BagSellS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Shop
    // -------------------------------------------------------------------------

    public static void SendShopList() => Net.Send(MessageConst.SHOP_LIST_C2S, new ShopListC2S());
    public static void SendShopBuy(int shopItemId) => Net.Send(MessageConst.SHOP_BUY_C2S, new ShopBuyC2S { ShopItemId = shopItemId });

    public static ShopListS2C ParseShopListS2C(byte[] body) => ShopListS2C.Parser.ParseFrom(body);
    public static ShopBuyS2C ParseShopBuyS2C(byte[] body) => ShopBuyS2C.Parser.ParseFrom(body);

    // -------------------------------------------------------------------------
    // Achievement
    // -------------------------------------------------------------------------

    public static void SendAchievementList() => Net.Send(MessageConst.ACHIEVEMENT_LIST_C2S, new AchievementListC2S());
    public static void SendAchievementClaim(int achievementId) => Net.Send(MessageConst.ACHIEVEMENT_CLAIM_C2S, new AchievementClaimC2S { Id = achievementId });

    public static AchievementListS2C ParseAchievementListS2C(byte[] body) => AchievementListS2C.Parser.ParseFrom(body);
    public static AchievementProgressS2C ParseAchievementProgressS2C(byte[] body) => AchievementProgressS2C.Parser.ParseFrom(body);
    public static AchievementUnlockS2C ParseAchievementUnlockS2C(byte[] body) => AchievementUnlockS2C.Parser.ParseFrom(body);
    public static AchievementClaimS2C ParseAchievementClaimS2C(byte[] body) => AchievementClaimS2C.Parser.ParseFrom(body);
}
