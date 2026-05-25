using UnityEngine;

/// <summary>
/// Global proxy for network lifecycle management.
/// - Registers CONNECT_S2C callback on startup (independent of any UI).
/// - Holds reconnect state; logic triggered by NetworkDisconnectedCommand / NetworkConnectedCommand.
/// - Reset timer: if reconnect hangs (NETWORK_CONNECTED never fires), auto-reset state after timeout.
/// </summary>
public class NetworkProxy : ProxyBase
{
    public new const string NAME = "NetworkProxy";

    public const int MAX_RECONNECT_ATTEMPTS = 3;
    public const float RECONNECT_TIMEOUT = 15f; // seconds before auto-reset

    public int  ReconnectAttempts { get; set; } = 0;
    public bool IsReconnecting    { get; set; } = false;

    private float _reconnectTimer = 0f;

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

    /// <summary>
    /// Called every frame by CommandBase.TryLuaHook or external tick.
    /// Auto-resets reconnect state if no NETWORK_CONNECTED received within timeout.
    /// </summary>
    public void TickReconnectTimeout()
    {
        if (!IsReconnecting) return;
        _reconnectTimer += Time.unscaledDeltaTime;
        if (_reconnectTimer >= RECONNECT_TIMEOUT)
        {
            Log.w($"Reconnect timed out after {RECONNECT_TIMEOUT}s. Resetting state.", NAME);
            IsReconnecting    = false;
            ReconnectAttempts = 0;
            NetworkManager.Instance.ClearPendingMessages();
        }
    }

    /// <summary>
    /// Reset reconnect state (called by NetworkConnectedCommand or on timeout).
    /// </summary>
    public void ResetReconnectState()
    {
        IsReconnecting    = false;
        ReconnectAttempts = 0;
        _reconnectTimer   = 0f;
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
