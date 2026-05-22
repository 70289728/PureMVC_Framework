using System.Collections.Generic;

/// <summary>
/// Announcement system proxy.
/// Announcement list is pushed by server after login; real-time updates via notify.
/// Registered at startup by RegisterProxyCommand in StartupMacroCommand.
/// </summary>
public class AnnounceProxy : ProxyBase
{
    public new const string NAME = "AnnounceProxy";

    public List<AnnounceInfo> Announces { get; private set; } = new List<AnnounceInfo>();

    public AnnounceProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Register(MessageConst.ANNOUNCE_LIST_S2C, OnAnnounceListS2C);
        disp.Register(MessageConst.ANNOUNCE_NOTIFY_S2C, OnAnnounceNotifyS2C);

        // Announcement data is pushed by server after login.
    }

    public override void OnRemove()
    {
        var disp = NetworkManager.Instance.Dispatcher;
        disp.Unregister(MessageConst.ANNOUNCE_LIST_S2C, OnAnnounceListS2C);
        disp.Unregister(MessageConst.ANNOUNCE_NOTIFY_S2C, OnAnnounceNotifyS2C);
    }

    #region Callbacks

    private void OnAnnounceListS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseAnnounceListS2C(body);
        Announces.Clear();
        Announces.AddRange(resp.Announces);
        Log.d($"Announce list: {Announces.Count} announces", NAME);
        SendNotification(NotificationConst.ANNOUNCE_LIST_UPDATED);
    }

    private void OnAnnounceNotifyS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseAnnounceNotifyS2C(body);
        if (resp.IsDelete)
        {
            Announces.RemoveAll(a => a.Id == resp.Announce.Id);
            Log.d($"Announce removed: {resp.Announce.Title}", NAME);
        }
        else
        {
            var idx = Announces.FindIndex(a => a.Id == resp.Announce.Id);
            if (idx >= 0) Announces[idx] = resp.Announce;
            else Announces.Insert(0, resp.Announce);
            Log.d($"Announce notify: {resp.Announce.Title}", NAME);

            // Server-pushed announcement — show as a modal dialog
            DialogManager.Instance.ShowInfo(resp.Announce.Title, resp.Announce.Content);
        }
        SendNotification(NotificationConst.ANNOUNCE_NOTIFY, resp);
    }

    #endregion
}
