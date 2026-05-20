using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Manages all connected clients with two indexes:
///   1. ClientId  -> ClientHandler  (always available after TCP connect)
///   2. AccountId -> ClientHandler  (available after login)
/// </summary>
public class ClientManager
{
    // Index by connection ID (always present)
    private readonly ConcurrentDictionary<string, ClientHandler> _clientsById
        = new ConcurrentDictionary<string, ClientHandler>();

    // Index by account ID (set after login, removed on disconnect)
    private readonly ConcurrentDictionary<long, ClientHandler> _clientsByAccount
        = new ConcurrentDictionary<long, ClientHandler>();

    #region Lifecycle

    /// <summary>Called by TCPServer when a TCP connection is established.</summary>
    public void OnClientConnected(ClientHandler handler)
    {
        _clientsById[handler.ClientId] = handler;
        Console.WriteLine($"[ClientManager] Client connected: {handler.ClientId} | Total: {_clientsById.Count}");
    }

    /// <summary>Called after a successful login to bind AccountId to the connection.</summary>
    public void OnClientLoggedIn(string clientId, long accountId)
    {
        if (!_clientsById.TryGetValue(clientId, out var handler))
        {
            Console.WriteLine($"[ClientManager] OnClientLoggedIn: ClientId {clientId} not found");
            return;
        }

        // If the account is already connected elsewhere, disconnect the old session
        if (_clientsByAccount.TryGetValue(accountId, out var existing) && existing.ClientId != clientId)
        {
            Console.WriteLine($"[ClientManager] Account {accountId} already connected on {existing.ClientId}, kicking old session");
            existing.Disconnect();
            _clientsByAccount.TryRemove(accountId, out _);
        }

        handler.AccountId = accountId;
        _clientsByAccount[accountId] = handler;
        Console.WriteLine($"[ClientManager] Account {accountId} bound to client {clientId}");
    }

    /// <summary>Called by TCPServer when a connection is closed (normally or abnormally).</summary>
    public void OnClientDisconnected(ClientHandler handler)
    {
        _clientsById.TryRemove(handler.ClientId, out _);

        if (handler.AccountId != 0)
        {
            _clientsByAccount.TryRemove(handler.AccountId, out _);
        }

        Console.WriteLine($"[ClientManager] Client disconnected: {handler.ClientId} | Total: {_clientsById.Count}");
    }

    #endregion

    #region Query

    public ClientHandler GetByClientId(string clientId)
    {
        _clientsById.TryGetValue(clientId, out var handler);
        return handler;
    }

    public ClientHandler GetByAccountId(long accountId)
    {
        _clientsByAccount.TryGetValue(accountId, out var handler);
        return handler;
    }

    public bool IsAccountOnline(long accountId) => _clientsByAccount.ContainsKey(accountId);

    public int ConnectionCount => _clientsById.Count;

    public int OnlinePlayerCount => _clientsByAccount.Count;

    /// <summary>Returns a snapshot of all connected clients.</summary>
    public IEnumerable<ClientHandler> GetAllClients() => _clientsById.Values.ToList();

    #endregion

    #region Send

    /// <summary>Send a message to a client by ClientId.</summary>
    public async Task SendToClientAsync(string clientId, EMessageType messageType, IMessage message)
    {
        var handler = GetByClientId(clientId);
        if (handler == null)
        {
            Console.WriteLine($"[ClientManager] SendToClient: ClientId {clientId} not found");
            return;
        }
        await handler.SendMessageAsync(messageType, message);
    }

    /// <summary>Send a message to a logged-in player by AccountId.</summary>
    public async Task SendToAccountAsync(long accountId, EMessageType messageType, IMessage message)
    {
        var handler = GetByAccountId(accountId);
        if (handler == null)
        {
            Console.WriteLine($"[ClientManager] SendToAccount: AccountId {accountId} not online");
            return;
        }
        await handler.SendMessageAsync(messageType, message);
    }

    /// <summary>Broadcast a message to all connected clients, optionally excluding one.</summary>
    public async Task BroadcastAsync(EMessageType messageType, IMessage message, string excludeClientId = null)
    {
        if (message == null)
        {
            Console.WriteLine("[ClientManager] Broadcast: message is null");
            return;
        }

        var targets = _clientsById.Values
            .Where(c => c.ClientId != excludeClientId)
            .ToList();

        foreach (var client in targets)
        {
            try
            {
                await client.SendMessageAsync(messageType, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientManager] Broadcast to {client.ClientId} failed: {ex.Message}");
            }
        }
        Console.WriteLine($"[ClientManager] Broadcast type {messageType} to {targets.Count} clients");
    }

    /// <summary>Broadcast a message to all logged-in players, optionally excluding one.</summary>
    public async Task BroadcastToPlayersAsync(EMessageType messageType, IMessage message, long? excludeAccountId = null)
    {
        if (message == null)
        {
            Console.WriteLine("[ClientManager] BroadcastToPlayers: message is null");
            return;
        }

        var targets = _clientsByAccount
            .Where(kv => kv.Key != excludeAccountId)
            .Select(kv => kv.Value)
            .ToList();

        foreach (var client in targets)
        {
            try
            {
                await client.SendMessageAsync(messageType, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientManager] BroadcastToPlayers to {client.AccountId} failed: {ex.Message}");
            }
        }
        Console.WriteLine($"[ClientManager] BroadcastToPlayers type {messageType} to {targets.Count} players");
    }

    #endregion
}