using System.Collections.Generic;

/// <summary>
/// Mail system proxy — handles mail list, read, claim, delete.
/// Mail data is pushed by server after login.
/// Registered at startup by RegisterProxyCommand in StartupMacroCommand.
/// </summary>
public class MailProxy : ProxyBase
{
    public new const string NAME = "MailProxy";

    public List<MailInfo> MailList { get; private set; } = new List<MailInfo>();
    public int UnreadCount { get; private set; }

    public MailProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Register(MessageConst.MAIL_LIST_S2C, OnMailListS2C);
        disp.Register(MessageConst.MAIL_READ_S2C, OnMailReadS2C);
        disp.Register(MessageConst.MAIL_CLAIM_S2C, OnMailClaimS2C);
        disp.Register(MessageConst.MAIL_DELETE_S2C, OnMailDeleteS2C);
        disp.Register(MessageConst.MAIL_NEW_NOTIFY_S2C, OnMailNewNotifyS2C);

        // Mail data is pushed by server after login.
    }

    public override void OnRemove()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Unregister(MessageConst.MAIL_LIST_S2C, OnMailListS2C);
        disp.Unregister(MessageConst.MAIL_READ_S2C, OnMailReadS2C);
        disp.Unregister(MessageConst.MAIL_CLAIM_S2C, OnMailClaimS2C);
        disp.Unregister(MessageConst.MAIL_DELETE_S2C, OnMailDeleteS2C);
        disp.Unregister(MessageConst.MAIL_NEW_NOTIFY_S2C, OnMailNewNotifyS2C);
    }

    #region Callbacks

    private void OnMailListS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseMailListS2C(body);
        MailList.Clear();
        MailList.AddRange(resp.Mails);
        UnreadCount = 0;
        foreach (var m in MailList) if (m.Status == 0) UnreadCount++;
        Log.d($"Mail list: {MailList.Count} mails, {UnreadCount} unread", NAME);
        SendNotification(NotificationConst.MAIL_LIST_UPDATED);

        // Update red dot
        RedDotManager.Instance.SetLeafCount("mail/new", UnreadCount);
    }

    private void OnMailReadS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseMailReadS2C(body);
        Log.d($"Read result: {(resp.Rst.Result ? "ok" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (resp.Rst.Result) SendNotification(NotificationConst.MAIL_LIST_UPDATED);
    }

    private void OnMailClaimS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseMailClaimS2C(body);
        Log.d($"Claim result: {(resp.Rst.Result ? $"ok, {resp.Claimed.Count} items" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (!resp.Rst.Result) return;
        // Refresh list after claim
        NetworkMessageHelper.SendMailList();
    }

    private void OnMailDeleteS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseMailDeleteS2C(body);
        Log.d($"Delete result: {(resp.Rst.Result ? "ok" : $"err={resp.Rst.ErrCode}")}", NAME);
        if (resp.Rst.Result) NetworkMessageHelper.SendMailList();
    }

    private void OnMailNewNotifyS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseMailNewNotifyS2C(body);
        Log.d($"New mail: {resp.Mail.Title}", NAME);
        MailList.Insert(0, resp.Mail);
        if (resp.Mail.Status == 0) UnreadCount++;
        SendNotification(NotificationConst.MAIL_NEW_NOTIFY, resp.Mail);
    }

    #endregion
}
