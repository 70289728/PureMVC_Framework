using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;

public class TCPServer : IDisposable, IAsyncDisposable
{
    private readonly IPAddress _ip;
    private readonly int _port;
    private readonly int _maxConnections;
    private const int DefaultMaxConnections = 100;

    private TcpListener _tcpListener;
    private CancellationTokenSource _cancellationTokenSource;

    public readonly ClientManager ClientManager = new ClientManager();
    public readonly IDataStore DataStore;

    private readonly List<AnnounceInfo> _announces = new List<AnnounceInfo>();
    private long _nextAnnounceId = 1;
    public IReadOnlyList<AnnounceInfo> Announces => _announces.AsReadOnly();

    public TCPServer(IPAddress address, int port, IDataStore dataStore, int maxConnections = DefaultMaxConnections)
    {
        _ip = address;
        _port = port;
        _maxConnections = maxConnections;
        DataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public async Task StartAsync()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            Console.WriteLine("[TCPServer] Server already running");
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _tcpListener = new TcpListener(_ip, _port);

        try
        {
            _tcpListener.Start();
            Console.WriteLine($"[TCPServer] Server started on {_ip}:{_port} (max connections: {_maxConnections})");
            await AcceptClientsAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[TCPServer] Accept loop cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCPServer] Startup error: {ex.Message}");
            await StopAsync();
        }
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync();

                if (ClientManager.ConnectionCount >= _maxConnections)
                {
                    Console.WriteLine($"[TCPServer] Max connections reached ({_maxConnections}), rejecting: {client.Client.RemoteEndPoint}");
                    client.Dispose();
                    continue;
                }

                _ = HandleClientConnectionAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[TCPServer] Stopped accepting connections");
                break;
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("[TCPServer] TcpListener disposed, stopping accept loop");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TCPServer] Accept error (continuing): {ex.Message}");
                continue;
            }
        }
    }

    private async Task HandleClientConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remoteEndPoint = client.Client.RemoteEndPoint.ToString();
        ClientHandler clientHandler = null;

        try
        {
            clientHandler = new ClientHandler(client, this, cancellationToken);
            ClientManager.OnClientConnected(clientHandler);

            await clientHandler.StartListeningAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCPServer] Client {remoteEndPoint} handler error: {ex.Message}");
        }
        finally
        {
            if (clientHandler != null)
            {
                ClientManager.OnClientDisconnected(clientHandler);
                clientHandler.Dispose();
            }
            client.Dispose();
        }
    }

    public Task BroadcastMessageAsync(EMessageType messageType, IMessage message, ClientHandler excludeClient = null)
        => ClientManager.BroadcastAsync(messageType, message, excludeClient?.ClientId);

    public Task SendToClientAsync(string clientId, EMessageType messageType, IMessage message)
        => ClientManager.SendToClientAsync(clientId, messageType, message);

    public Task StopAsync()
    {
        if (_cancellationTokenSource == null)
        {
            Console.WriteLine("[TCPServer] Server not running");
            return Task.CompletedTask;
        }

        try
        {
            _cancellationTokenSource.Cancel();
            Console.WriteLine("[TCPServer] Stopping server...");

            foreach (var handler in ClientManager.GetAllClients())
            {
                handler.Disconnect();
            }

            if (_tcpListener != null)
            {
                _tcpListener.Stop();
                _tcpListener = null;
            }

            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            Console.WriteLine("[TCPServer] Server stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TCPServer] Stop error: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }

    ~TCPServer()
    {
        Dispose(false);
    }

    #region Announce

    public void AddAnnounce(string title, string content, int priority = 0)
    {
        var a = new AnnounceInfo
        {
            Id = _nextAnnounceId++,
            Title = title,
            Content = content,
            Priority = priority,
            EndTime = 0
        };
        _announces.Add(a);
        Console.WriteLine($"[TCPServer] Announce added: [{a.Id}] {title}");
        _ = BroadcastAnnounceAsync(a, false);
    }

    public void RemoveAnnounce(long id)
    {
        var a = _announces.Find(x => x.Id == id);
        if (a == null) return;
        _announces.Remove(a);
        Console.WriteLine($"[TCPServer] Announce removed: [{id}] {a.Title}");
        _ = BroadcastAnnounceAsync(a, true);
    }

    private async Task BroadcastAnnounceAsync(AnnounceInfo a, bool isDelete)
    {
        var notify = new AnnounceNotifyS2C { Announce = a, IsDelete = isDelete };
        foreach (var client in ClientManager.GetAllClients())
        {
            try { await client.SendMessageAsync(EMessageType.ANNOUNCE_NOTIFY_S2C, notify); }
            catch { }
        }
    }

    public async Task PushAnnounceListAsync(ClientHandler client)
    {
        var ack = new AnnounceListS2C();
        ack.Announces.AddRange(_announces);
        await client.SendMessageAsync(EMessageType.ANNOUNCE_LIST_S2C, ack);
    }

    #endregion
}