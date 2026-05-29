using PureMVC.Interfaces;

/// <summary>
/// Triggered by NETWORK_DISCONNECTED notification.
/// Shows reconnect dialog and handles retry logic.
/// </summary>
public class NetworkDisconnectedCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var proxy = GetProxy<NetworkProxy>(NetworkProxy.NAME);
        if (proxy.IsReconnecting) return;

        proxy.IsReconnecting = true;

        string message = proxy.ReconnectAttempts > 0
            ? $"Connection lost (attempt {proxy.ReconnectAttempts}/{NetworkProxy.MAX_RECONNECT_ATTEMPTS}). Try again?"
            : "Server connection lost. Reconnect?";

        DialogManager.Instance.ShowConfirm(
            "Network Disconnected",
            message,
            onConfirm: () => HandleChoice(proxy, true),
            onCancel:  () => HandleChoice(proxy, false),
            confirmText: "Reconnect",
            cancelText: "Cancel");
    }

    private void HandleChoice(NetworkProxy proxy, bool confirm)
    {
        if (confirm)
        {
            proxy.ReconnectAttempts++;
            if (proxy.ReconnectAttempts > NetworkProxy.MAX_RECONNECT_ATTEMPTS)
            {
                Log.w("Max reconnect attempts reached. Giving up.", "NetworkDisconnectedCommand");
                proxy.IsReconnecting    = false;
                proxy.ReconnectAttempts = 0;
                NetworkManager.Instance.ClearPendingMessages();
                return;
            }
            Log.d($"Reconnecting (attempt {proxy.ReconnectAttempts}/{NetworkProxy.MAX_RECONNECT_ATTEMPTS})...", "NetworkDisconnectedCommand");
            NetworkManager.Instance.Connect();
        }
        else
        {
            Log.d("User cancelled reconnect. Clearing pending messages.", "NetworkDisconnectedCommand");
            proxy.IsReconnecting    = false;
            proxy.ReconnectAttempts = 0;
            NetworkManager.Instance.ClearPendingMessages();
        }
    }
}

/// <summary>
/// Triggered by NETWORK_CONNECTED notification.
/// If this is a reconnect, sends LOGIN_C2S first to rebind AccountId on the new TCP connection.
/// UserProxy.OnLoginS2C completes the reconnect handshake (flush + data refresh).
/// </summary>
public class NetworkConnectedCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var proxy = GetProxy<NetworkProxy>(NetworkProxy.NAME);
        if (!proxy.IsReconnecting) return;

        Log.d($"Reconnect: new TCP connection established. AccountId={NetworkManager.CurrentAccountId}.", "NetworkConnectedCommand");

        if (NetworkManager.HasCachedLogin)
        {
            Log.d("Reconnect: sending login to rebind account on new connection...", "NetworkConnectedCommand");
            NetworkManager.Instance.SendCachedLogin();
            // Keep IsReconnecting=true — UserProxy.OnLoginS2C will complete the handshake.
        }
        else
        {
            // No cached credentials (legacy scenario or first connect), flush directly
            Log.w("Reconnect: no cached credentials, flushing pending without login.", "NetworkConnectedCommand");
            proxy.ResetReconnectState();
            NetworkManager.Instance.FlushPendingMessages();
            NetworkMessageHelper.SendBagList();
            Facade.SendNotification(NetworkNotificationConst.NETWORK_RECONNECTED);
        }
    }
}

/// <summary>
/// Triggered by NETWORK_ERROR notification.
/// Handles connection errors that are not clean disconnect events.
/// </summary>
public class NetworkErrorCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var errorMsg = notification.Body as string ?? "Connection error";
        Log.w($"Network error: {errorMsg}", "NetworkErrorCommand");

        var proxy = GetProxy<NetworkProxy>(NetworkProxy.NAME);
        // If currently reconnecting and the reconnection itself failed, reset state
        // so the user can retry without waiting for the 15s timeout.
        if (proxy.IsReconnecting)
        {
            Log.w("Reconnect attempt failed. Resetting reconnect state.", "NetworkErrorCommand");
            proxy.ResetReconnectState();
            NetworkManager.Instance.ClearPendingMessages();
        }
    }
}
