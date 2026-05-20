using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using PureMVC.Patterns.Facade;
using Google.Protobuf;

/// <summary>
/// TCP network client manager.
/// - async/await connect with configurable timeout.
/// - Receive loop runs on a background Task; decoded packets are queued to a ConcurrentQueue.
/// - Update() drains the packet queue on the Unity main thread and dispatches via MessageDispatcher.
/// - Send loop runs on a background Task to avoid blocking the main thread.
/// - Heartbeat sent automatically at a configurable interval.
/// - Fires PureMVC notifications on connect / disconnect / error.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    #region Singleton
    private static NetworkManager instance;
    public static NetworkManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("NetworkManager");
                instance = go.AddComponent<NetworkManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
    #endregion

    #region Configuration
    [Header("Server Settings")]
    public string serverIP   = "127.0.0.1";
    public int    serverPort = 5060;
    public int    connectTimeoutMs  = 5000;
    public int    maxMessageSize    = 1024 * 1024;

    [Header("Heartbeat")]
    public bool  enableHeartbeat      = true;
    public float heartbeatInterval    = 5f;
    #endregion

    #region Public API
    public MessageDispatcher Dispatcher { get; private set; }
    public bool IsConnected { get; private set; }
    #endregion

    #region Private Fields
    private TcpClient      tcpClient;
    private NetworkStream  networkStream;

    private CancellationTokenSource cts;

    private readonly PacketHandler packetHandler = new PacketHandler();

    private readonly ConcurrentQueue<(int msgId, byte[] body)> receiveQueue =
        new ConcurrentQueue<(int, byte[])>();
    private readonly ConcurrentQueue<byte[]> sendQueue =
        new ConcurrentQueue<byte[]>();

    // Pending frames buffered while disconnected; flushed after reconnect
    private readonly System.Collections.Generic.Queue<(int msgId, byte[] body)> pendingQueue =
        new System.Collections.Generic.Queue<(int, byte[])>();

    private float heartbeatTimer;

    // True when disconnect is intentional (user quit / manual Disconnect call)
    // Passive disconnects (server drop / network error) leave this false
    private bool isIntentionalDisconnect = false;

    // Sentinel msgId for signalling unexpected disconnect on the main thread
    private const int SENTINEL_DISCONNECT = int.MinValue;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance   = this;
        Dispatcher = new MessageDispatcher();
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        HandleHeartbeat();
        DrainReceiveQueue();
    }

    private void OnDestroy()         => DisconnectInternal();
    private void OnApplicationQuit() => DisconnectInternal();
    #endregion

    #region Connect / Disconnect
    /// <summary>
    /// Connect using the inspector-configured serverIP and serverPort.
    /// If serverIP is "127.0.0.1" or "localhost", tries to resolve from HotUpdateConfig first.
    /// </summary>
    public void Connect()
    {
        string host = serverIP;
        // Auto-resolve from hot update server URL for non-localhost scenarios
        if (host == "127.0.0.1" || host == "localhost" || host == "0.0.0.0")
        {
            var hotUpdateConfig = Resources.Load<HotUpdateConfig>("HotUpdateConfig");
            if (hotUpdateConfig != null && !string.IsNullOrEmpty(hotUpdateConfig.serverBaseUrl))
            {
                string extracted = ExtractHostFromUrl(hotUpdateConfig.serverBaseUrl);
                if (!string.IsNullOrEmpty(extracted) && extracted != "localhost" && extracted != "127.0.0.1" && extracted != "0.0.0.0")
                {
                    host = extracted;
                    Log.d($"Resolved server IP from HotUpdateConfig: {host}", "NetworkManager");
                }
            }
        }
        Connect(host, serverPort);
    }

    /// <summary>
    /// Extract host from URL like "http://10.219.60.142:8080" → "10.219.60.142".
    /// </summary>
    private static string ExtractHostFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        // Remove protocol
        int protocolEnd = url.IndexOf("://", StringComparison.Ordinal);
        string hostPart = protocolEnd >= 0 ? url.Substring(protocolEnd + 3) : url;
        // Remove port and path
        int portStart = hostPart.IndexOf(':', StringComparison.Ordinal);
        if (portStart >= 0) hostPart = hostPart.Substring(0, portStart);
        int pathStart = hostPart.IndexOf('/', StringComparison.Ordinal);
        if (pathStart >= 0) hostPart = hostPart.Substring(0, pathStart);
        return hostPart;
    }

    /// <summary>
    /// Async connect with timeout. Sends NETWORK_CONNECTED on success, NETWORK_ERROR on failure.
    /// </summary>
    public async void Connect(string host, int port)
    {
        if (IsConnected)
        {
            Log.w("Already connected. Call Disconnect() first.", "NetworkManager");
            return;
        }

        isIntentionalDisconnect = false;
        ResetState();

        try
        {
            Log.d($"Connecting to {host}:{port}...", "NetworkManager");

            tcpClient = new TcpClient();
            cts       = new CancellationTokenSource();

            var connectTask  = tcpClient.ConnectAsync(host, port);
            var timeoutTask  = Task.Delay(connectTimeoutMs, cts.Token);
            var completed    = await Task.WhenAny(connectTask, timeoutTask);

            if (completed == timeoutTask)
            {
                Log.e($"Connection timed out after {connectTimeoutMs}ms.", "NetworkManager");
                CleanupSocket();
                Facade.Instance.SendNotification(NetworkNotificationConst.NETWORK_ERROR, "Connection timed out.");
                return;
            }

            await connectTask; // re-throw if faulted

            networkStream = tcpClient.GetStream();
            IsConnected   = true;
            heartbeatTimer = 0f;
            packetHandler.Clear();

            Log.d($"Connected to {host}:{port}.", "NetworkManager");
            Facade.Instance.SendNotification(NetworkNotificationConst.NETWORK_CONNECTED);

            // Start background loops
            _ = ReceiveLoopAsync(cts.Token);
            _ = SendLoopAsync(cts.Token);
        }
        catch (Exception e)
        {
            Log.e($"Connection failed: {e.Message}", "NetworkManager");
            CleanupSocket();
            Facade.Instance.SendNotification(NetworkNotificationConst.NETWORK_ERROR, e.Message);
        }
    }

    /// <summary>
    /// Disconnect from the server intentionally (e.g. logout, scene change).
    /// Does NOT fire the reconnect dialog.
    /// </summary>
    public void Disconnect()
    {
        isIntentionalDisconnect = true;
        DisconnectInternal();
    }

    // Called by OnDestroy / OnApplicationQuit — silent cleanup, no notifications.
    private void DisconnectInternal()
    {
        if (!IsConnected && tcpClient == null)
            return;

        isIntentionalDisconnect = true;
        cts?.Cancel();
        CleanupSocket();
        Log.d("Disconnected (intentional).", "NetworkManager");
        // No NETWORK_DISCONNECTED notification — intentional quit should not trigger reconnect
    }

    private void ResetState()
    {
        IsConnected    = false;
        heartbeatTimer = 0f;
        packetHandler.Clear();
        networkStream  = null;
        tcpClient      = null;
    }

    private void CleanupSocket()
    {
        IsConnected = false;
        try { networkStream?.Close(); }  catch { }
        try { tcpClient?.Close(); }      catch { }
        networkStream = null;
        tcpClient     = null;
        packetHandler.Clear();
    }
    #endregion

    #region Send
    /// <summary>
    /// Build a framed packet and enqueue it for the send task.
    /// Frame: [msgId(4 big-endian)][bodyLen(4 big-endian)][body]
    /// </summary>
    public void Send(int msgId, byte[] body = null)
    {
        int bodyLen = body != null ? body.Length : 0;
        if (bodyLen > maxMessageSize)
        {
            Log.e($"Message body too large: {bodyLen} bytes (max {maxMessageSize}).", "NetworkManager");
            return;
        }

        if (!IsConnected)
        {
            if (isIntentionalDisconnect)
            {
                Log.w($"Send ignored: intentional disconnect in progress (msgId={msgId}).", "NetworkManager");
                return;
            }
            // Passive disconnect: buffer and trigger reconnect flow
            Log.w($"Not connected. Buffering msgId={msgId} for after reconnect.", "NetworkManager");
            pendingQueue.Enqueue((msgId, body));
            Facade.Instance.SendNotification(NetworkNotificationConst.NETWORK_DISCONNECTED);
            return;
        }

        EnqueueFrame(msgId, body, bodyLen);
    }

    /// <summary>
    /// Flush all buffered pending messages into the send queue.
    /// Called automatically after a successful reconnect.
    /// </summary>
    public void FlushPendingMessages()
    {
        int count = pendingQueue.Count;
        if (count == 0) return;

        Log.d($"Flushing {count} pending message(s) after reconnect.", "NetworkManager");
        while (pendingQueue.Count > 0)
        {
            var (msgId, body) = pendingQueue.Dequeue();
            int bodyLen = body != null ? body.Length : 0;
            EnqueueFrame(msgId, body, bodyLen);
        }
    }

    /// <summary>
    /// Discard all buffered pending messages (e.g. user cancels reconnect).
    /// </summary>
    public void ClearPendingMessages()
    {
        int count = pendingQueue.Count;
        pendingQueue.Clear();
        if (count > 0)
            Log.d($"Cleared {count} pending message(s).", "NetworkManager");
    }

    private void EnqueueFrame(int msgId, byte[] body, int bodyLen)
    {
        byte[] frame = new byte[8 + bodyLen];
        WriteBigEndianInt32(frame, 0, msgId);
        WriteBigEndianInt32(frame, 4, bodyLen);
        if (bodyLen > 0)
            Buffer.BlockCopy(body, 0, frame, 8, bodyLen);
        sendQueue.Enqueue(frame);
    }

    /// <summary>
    /// Serialize a Protobuf message and send it with the given message ID.
    /// </summary>
    public void Send<T>(int msgId, T proto) where T : IMessage
    {
        Send(msgId, proto.ToByteArray());
    }
    #endregion

    #region Background Tasks
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        byte[] buf = new byte[4096];
        try
        {
            while (!token.IsCancellationRequested && IsConnected)
            {
                int bytesRead = await networkStream.ReadAsync(buf, 0, buf.Length, token);
                if (bytesRead == 0)
                {
                    // Server closed connection gracefully
                    HandleUnexpectedDisconnect();
                    return;
                }

                packetHandler.Append(buf, bytesRead);

                while (packetHandler.TryReadPacket(out int msgId, out byte[] body))
                {
                    // Validate message size
                    if (body != null && body.Length > maxMessageSize)
                    {
                        Log.e($"Received oversized message: {body.Length} bytes. Closing connection.", "NetworkManager");
                        HandleUnexpectedDisconnect();
                        return;
                    }
                    receiveQueue.Enqueue((msgId, body));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation on Disconnect()
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
            {
                Log.e($"Receive error: {e.Message}", "NetworkManager");
                HandleUnexpectedDisconnect();
            }
        }
    }

    private async Task SendLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && IsConnected)
            {
                if (sendQueue.TryDequeue(out byte[] frame))
                {
                    await networkStream.WriteAsync(frame, 0, frame.Length, token);
                    await networkStream.FlushAsync(token);
                }
                else
                {
                    await Task.Delay(1, token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation on Disconnect()
        }
        catch (Exception e)
        {
            if (!token.IsCancellationRequested)
                Log.e($"Send error: {e.Message}", "NetworkManager");
        }
    }

    private void HandleUnexpectedDisconnect()
    {
        cts?.Cancel();
        CleanupSocket();
        receiveQueue.Enqueue((SENTINEL_DISCONNECT, null));
    }
    #endregion

    #region Main Thread Drain
    private void DrainReceiveQueue()
    {
        while (receiveQueue.TryDequeue(out var packet))
        {
            if (packet.msgId == SENTINEL_DISCONNECT)
            {
                if (!isIntentionalDisconnect)
                {
                    Log.w("Server connection lost.", "NetworkManager");
                    Facade.Instance.SendNotification(NetworkNotificationConst.NETWORK_DISCONNECTED);
                }
                continue;
            }
            Dispatcher.Dispatch(packet.msgId, packet.body);
        }
    }
    #endregion

    #region Heartbeat
    private void HandleHeartbeat()
    {
        if (!enableHeartbeat || !IsConnected) return;

        heartbeatTimer += Time.deltaTime;
        if (heartbeatTimer >= heartbeatInterval)
        {
            heartbeatTimer = 0f;
            var heartbeat = new HeartbeatC2S { AccountId = 99999 };
            Send(MessageConst.HEARTBEAT_C2S, heartbeat);
            Log.d("Heartbeat sent.", "NetworkManager");
        }
    }
    #endregion

    #region Helpers
    private static void WriteBigEndianInt32(byte[] buf, int offset, int value)
    {
        buf[offset]     = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)(value);
    }
    #endregion
}