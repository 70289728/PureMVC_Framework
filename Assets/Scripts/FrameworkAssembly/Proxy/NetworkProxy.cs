using UnityEngine;

/// <summary>
/// Global proxy for network lifecycle management.
/// - Registers CONNECT_S2C callback on startup (independent of any UI).
/// - Holds reconnect state; logic triggered by NetworkDisconnectedCommand / NetworkConnectedCommand.
/// - Reset timer: if reconnect hangs (NETWORK_CONNECTED never fires), auto-reset state after timeout.
///   Tick driven by UpdateManager (registered in OnRegister), no longer polled by NetworkManager.
/// </summary>
public class NetworkProxy : ProxyBase, IUpdatable
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
        // Register self with UpdateManager for reconnect timeout ticking.
        // Owns its own tick instead of being polled by NetworkManager.
        UpdateManager.Instance.Register(this, UpdateType.Update, UpdateFrequency.Low);
    }

    public override void OnRemove()
    {
        NetworkManager.Instance.Dispatcher.Unregister(MessageConst.CONNECT_S2C, OnConnectS2C);
        NetworkManager.Instance.Dispatcher.Unregister(MessageConst.HEARTBEAT_S2C, OnHeartbeatS2C);
        UpdateManager.Instance.Unregister(this, UpdateType.Update);
    }

    #region IUpdatable
    /// <summary>
    /// Only active while reconnecting — UpdateManager will skip this entry otherwise.
    /// </summary>
    public bool IsUpdateActive => IsReconnecting;

    public void OnUpdate(float deltaTime)
    {
        TickReconnectTimeout(deltaTime);
    }

    public void OnFixedUpdate(float fixedDeltaTime) { }
    public void OnLateUpdate(float deltaTime) { }
    #endregion

    /// <summary>
    /// Auto-resets reconnect state if no NETWORK_CONNECTED received within timeout.
    /// </summary>
    private void TickReconnectTimeout(float deltaTime)
    {
        if (!IsReconnecting) return;
        // Use unscaledDeltaTime so reconnect logic is unaffected by Time.timeScale changes
        _reconnectTimer += Time.unscaledDeltaTime;
        if (_reconnectTimer >= RECONNECT_TIMEOUT)
        {
            Log.w($"Reconnect timed out after {RECONNECT_TIMEOUT}s. Resetting state.", NAME);
            IsReconnecting    = false;
            ReconnectAttempts = 0;
            _reconnectTimer   = 0f;
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
