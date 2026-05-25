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
/// If this is a reconnect, flushes buffered pending messages.
/// </summary>
public class NetworkConnectedCommand : CommandBase
{
    protected override void OnExecute(INotification notification)
    {
        var proxy = GetProxy<NetworkProxy>(NetworkProxy.NAME);
        if (!proxy.IsReconnecting) return;

        proxy.IsReconnecting    = false;
        proxy.ReconnectAttempts = 0;
        Log.d("Reconnected successfully. Flushing pending messages.", "NetworkConnectedCommand");
        NetworkManager.Instance.FlushPendingMessages();

        // Re-request server-synced data after reconnect (data may be stale after disconnect).
        NetworkMessageHelper.SendBagList();
        Facade.SendNotification(NetworkNotificationConst.NETWORK_RECONNECTED);
    }
}
