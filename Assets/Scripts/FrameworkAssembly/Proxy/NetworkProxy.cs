/// <summary>
/// Global proxy for network lifecycle management.
/// - Registers CONNECT_S2C callback on startup (independent of any UI).
/// - Holds reconnect state; logic triggered by NetworkDisconnectedCommand / NetworkConnectedCommand.
/// </summary>
public class NetworkProxy : ProxyBase
{
    public new const string NAME = "NetworkProxy";

    public const int MAX_RECONNECT_ATTEMPTS = 3;
    public int  ReconnectAttempts { get; set; } = 0;
    public bool IsReconnecting    { get; set; } = false;

    public NetworkProxy() : base(NAME, null) { }

    public override void OnRegister()
    {
        NetworkManager.Instance.Dispatcher.Register(MessageConst.CONNECT_S2C, OnConnectS2C);
        NetworkManager.Instance.Dispatcher.Register(MessageConst.HEARTBEAT_S2C, OnHeartbeatS2C);
    }

    public override void OnRemove()
    {
        NetworkManager.Instance.Dispatcher.Unregister(MessageConst.CONNECT_S2C, OnConnectS2C);
        NetworkManager.Instance.Dispatcher.Unregister(MessageConst.HEARTBEAT_S2C, OnHeartbeatS2C);
    }

    private void OnConnectS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseConnectS2C(body);
        if (resp.Rst.Result)
            Log.d("Server handshake accepted.", NAME);
        else
            Log.w($"Server handshake rejected, errCode={resp.Rst.ErrCode}", NAME);
    }

    private void OnHeartbeatS2C(byte[] body)
    {
        var resp = NetworkMessageHelper.ParseHeartbeatS2C(body);
        if (resp.Rst.Result)
            Log.d("Heartbeat pong received.", NAME);
        else
            Log.w($"Heartbeat pong failed, errCode={resp.Rst.ErrCode}", NAME);
    }
}

/// <summary>
/// Data passed with the NETWORK_DISCONNECTED_DIALOG notification.
/// The UI dialog reads this to wire up its confirm/cancel buttons.
/// </summary>
public class ReconnectDialogData
{
    public string Message   { get; }
    public System.Action OnConfirm { get; }
    public System.Action OnCancel  { get; }

    public ReconnectDialogData(string message, System.Action onConfirm, System.Action onCancel)
    {
        Message   = message;
        OnConfirm = onConfirm;
        OnCancel  = onCancel;
    }
}
