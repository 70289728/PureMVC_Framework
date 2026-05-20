using System.Collections.Generic;

/// <summary>
/// Friend system proxy — handles friend list, apply list, block list, and related network callbacks.
/// Registered at startup by RegisterProxyCommand in StartupMacroCommand.
/// </summary>
public class FriendProxy : ProxyBase
{
    public new const string NAME = "FriendProxy";

    // ── Runtime data ──
    public List<FriendInfo> FriendList { get; private set; } = new List<FriendInfo>();
    public List<FriendApplyInfo> ApplyList { get; private set; } = new List<FriendApplyInfo>();
    public List<BlockInfo> BlockList { get; private set; } = new List<BlockInfo>();
    public FriendSearchS2C LastSearchResult { get; private set; }
    public const int MaxFriendCount = 50;

    public FriendProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Register(MessageConst.FRIEND_SEARCH_S2C, OnFriendSearchS2C);
        disp.Register(MessageConst.FRIEND_APPLY_S2C, OnFriendApplyS2C);
        disp.Register(MessageConst.FRIEND_APPLY_LIST_S2C, OnFriendApplyListS2C);
        disp.Register(MessageConst.FRIEND_REPLY_S2C, OnFriendReplyS2C);
        disp.Register(MessageConst.FRIEND_LIST_S2C, OnFriendListS2C);
        disp.Register(MessageConst.FRIEND_DELETE_S2C, OnFriendDeleteS2C);
        disp.Register(MessageConst.FRIEND_REMARK_S2C, OnFriendRemarkS2C);
        disp.Register(MessageConst.FRIEND_BLOCK_S2C, OnFriendBlockS2C);
        disp.Register(MessageConst.FRIEND_BLOCK_LIST_S2C, OnFriendBlockListS2C);
        disp.Register(MessageConst.FRIEND_ONLINE_NOTIFY_S2C, OnFriendOnlineNotifyS2C);

        // Friend data is pushed by server after login — no client-side fetch needed.
    }

    public override void OnRemove()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Unregister(MessageConst.FRIEND_SEARCH_S2C, OnFriendSearchS2C);
        disp.Unregister(MessageConst.FRIEND_APPLY_S2C, OnFriendApplyS2C);
        disp.Unregister(MessageConst.FRIEND_APPLY_LIST_S2C, OnFriendApplyListS2C);
        disp.Unregister(MessageConst.FRIEND_REPLY_S2C, OnFriendReplyS2C);
        disp.Unregister(MessageConst.FRIEND_LIST_S2C, OnFriendListS2C);
        disp.Unregister(MessageConst.FRIEND_DELETE_S2C, OnFriendDeleteS2C);
        disp.Unregister(MessageConst.FRIEND_REMARK_S2C, OnFriendRemarkS2C);
        disp.Unregister(MessageConst.FRIEND_BLOCK_S2C, OnFriendBlockS2C);
        disp.Unregister(MessageConst.FRIEND_BLOCK_LIST_S2C, OnFriendBlockListS2C);
        disp.Unregister(MessageConst.FRIEND_ONLINE_NOTIFY_S2C, OnFriendOnlineNotifyS2C);
    }

    #region Callbacks

    private void OnFriendSearchS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendSearchS2C(body);
        Log.d($"Search result: {(resp.Rst.Result ? resp.PlayerName : "not found")}", NAME);
        if (!resp.Rst.Result) return;
        LastSearchResult = resp;
        SendNotification(NotificationConst.FRIEND_SEARCH_RESULT, resp);
    }

    private void OnFriendApplyS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendApplyS2C(body);
        Log.d($"Apply result: {(resp.Rst.Result ? "ok" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (!resp.Rst.Result) return;
        SendNotification(NotificationConst.FRIEND_APPLY_RESULT, resp.Rst.Result);
    }

    private void OnFriendApplyListS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendApplyListS2C(body);
        ApplyList.Clear();
        ApplyList.AddRange(resp.Applies);
        Log.d($"Apply list: {ApplyList.Count} pending", NAME);
        SendNotification(NotificationConst.FRIEND_APPLY_LIST_UPDATED);
    }

    private void OnFriendReplyS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendReplyS2C(body);
        Log.d($"Reply result: {(resp.Rst.Result ? "ok" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (!resp.Rst.Result) return;
        NetworkMessageHelper.SendFriendList();
        NetworkMessageHelper.SendFriendApplyList();
        SendNotification(NotificationConst.FRIEND_REPLY_RESULT, true);
    }

    private void OnFriendListS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendListS2C(body);
        FriendList.Clear();
        FriendList.AddRange(resp.Friends);
        Log.d($"Friend list: {FriendList.Count} friends", NAME);
        SendNotification(NotificationConst.FRIEND_LIST_UPDATED);
    }

    private void OnFriendDeleteS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendDeleteS2C(body);
        Log.d($"Delete result: {(resp.Rst.Result ? "ok" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (!resp.Rst.Result) return;
        NetworkMessageHelper.SendFriendList();
        SendNotification(NotificationConst.FRIEND_DELETE_RESULT, true);
    }

    private void OnFriendRemarkS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendRemarkS2C(body);
        Log.d($"Remark result: {(resp.Rst.Result ? "ok" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (!resp.Rst.Result) return;
        NetworkMessageHelper.SendFriendList();
        SendNotification(NotificationConst.FRIEND_REMARK_RESULT, true);
    }

    private void OnFriendBlockS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendBlockS2C(body);
        Log.d($"Block result: {(resp.Rst.Result ? "ok" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (!resp.Rst.Result) return;
        NetworkMessageHelper.SendFriendBlockList();
        SendNotification(NotificationConst.FRIEND_BLOCK_RESULT, true);
    }

    private void OnFriendBlockListS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendBlockListS2C(body);
        BlockList.Clear();
        BlockList.AddRange(resp.Blocks);
        Log.d($"Block list: {BlockList.Count} blocked", NAME);
        SendNotification(NotificationConst.FRIEND_BLOCK_LIST_UPDATED);
    }

    private void OnFriendOnlineNotifyS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseFriendOnlineNotifyS2C(body);
        Log.d($"Online: playerId={resp.PlayerId}, online={resp.IsOnline}", NAME);
        for (int i = 0; i < FriendList.Count; i++)
        {
            if (FriendList[i].PlayerId == resp.PlayerId)
            {
                FriendList[i].IsOnline = resp.IsOnline;
                break;
            }
        }
        SendNotification(NotificationConst.FRIEND_ONLINE_UPDATED, resp);
    }

    #endregion
}
