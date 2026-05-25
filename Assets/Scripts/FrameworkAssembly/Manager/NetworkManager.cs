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

    // Backing field with volatile for cross-thread visibility
    private volatile bool _isConnected;
    public bool IsConnected => _isConnected;
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
    private readonly ConcurrentQueue<(int msgId, byte[] body)> pendingQueue =
        new ConcurrentQueue<(int, byte[])>();
    private float heartbeatTimer;

    // True when disconnect is intentional (user quit / manual Disconnect call)
    // Passive disconnects (server drop / network error) leave this false
    private volatile bool isIntentionalDisconnect = false;

    // Lock to guard IsConnected + CleanupSocket transitions and Send() TOCTOU
    private readonly object _connectionLock = new object();

    // Interlocked flag to prevent double-fire HandleUnexpectedDisconnect
    private int _isDisconnecting = 0;

    // Prevent duplicate NETWORK_DISCONNECTED notifications while disconnected
    private volatile bool _disconnectNotified = false;

    // Send queue backpressure: drop frames when queue exceeds this limit
    private const int SendQueueMaxSize = 1000;

    // Sentinel msgId for signalling unexpected disconnect on the main thread
    private const int SENTINEL_DISCONNECT = int.MinValue;

    // Current logged-in account ID for heartbeat (set after login success)
    private static long _currentAccountId = 0;
    public static long CurrentAccountId
    {
        get => Interlocked.Read(ref _currentAccountId);
        set => Interlocked.Exchange(ref _currentAccountId, value);
    }
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
    /// </summary>
    public void Connect()
    {
        _ = ConnectTaskAsync(serverIP, serverPort);
    }

    /// <summary>
    /// Async connect with timeout. Sends NETWORK_CONNECTED on success, NETWORK_ERROR on failure.
    /// </summary>
    public async Task ConnectTaskAsync(string host, int port)
    {
        if (_isConnected)
        {
            Log.w("Already connected. Call Disconnect() first.", "NetworkManager");
            return;
        }

        isIntentionalDisconnect = false;
        _disconnectNotified = false;
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
            _isConnected   = true;
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
        if (!_isConnected && tcpClient == null)
            return;

        isIntentionalDisconnect = true;
        cts?.Cancel();
        CleanupSocket();
        Log.d("Disconnected (intentional).", "NetworkManager");
        // No NETWORK_DISCONNECTED notification — intentional quit should not trigger reconnect
    }

    private void ResetState()
    {
        _isConnected    = false;
        heartbeatTimer = 0f;
        packetHandler.Clear();
        networkStream  = null;
        tcpClient      = null;
    }

    private void CleanupSocket()
    {
        lock (_connectionLock)
        {
            _isConnected = false;
            try { networkStream?.Close(); }  catch { }
            try { tcpClient?.Close(); }      catch { }
            networkStream = null;
            tcpClient     = null;
        }
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

        bool shouldNotifyDisconnect = false;
        lock (_connectionLock)
        {
            if (!_isConnected)
            {
                if (isIntentionalDisconnect)
                {
                    Log.w($"Send ignored: intentional disconnect in progress (msgId={msgId}).", "NetworkManager");
                    return;
                }
                // Passive disconnect: buffer and trigger reconnect flow
                Log.w($"Not connected. Buffering msgId={msgId} for after reconnect.", "NetworkManager");
                pendingQueue.Enqueue((msgId, body));
                if (!_disconnectNotified)
                {
                    _disconnectNotified = true;
                    shouldNotifyDisconnect = true;
                }
                return;
            }

            EnqueueFrame(msgId, body, bodyLen);
        }

        // Notify outside lock to prevent re-entrant deadlock
        if (shouldNotifyDisconnect)
        {
            Facade.Instance.SendNotification(NetworkNotificationConst.NETWORK_DISCONNECTED);
        }
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
        while (pendingQueue.TryDequeue(out var pending))
        {
            int bodyLen = pending.body != null ? pending.body.Length : 0;
            EnqueueFrame(pending.msgId, pending.body, bodyLen);
        }
    }

    /// <summary>
    /// Discard all buffered pending messages (e.g. user cancels reconnect).
    /// </summary>
    public void ClearPendingMessages()
    {
        int count = 0;
        while (pendingQueue.TryDequeue(out _)) count++;
        if (count > 0)
            Log.d($"Cleared {count} pending message(s).", "NetworkManager");
    }

    private void EnqueueFrame(int msgId, byte[] body, int bodyLen)
    {
        if (sendQueue.Count >= SendQueueMaxSize)
        {
            Log.e($"Send queue full ({SendQueueMaxSize} frames). Dropping msgId={msgId}.", "NetworkManager");
            return;
        }
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
            while (!token.IsCancellationRequested && _isConnected)
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
            while (!token.IsCancellationRequested && _isConnected)
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
        // Prevent double-fire from concurrent error paths
        if (Interlocked.Exchange(ref _isDisconnecting, 1) == 1)
            return;

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
            var heartbeat = new HeartbeatC2S { AccountId = CurrentAccountId };
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