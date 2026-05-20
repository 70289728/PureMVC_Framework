# ProtoServer - Code Map

> Last updated: 2026-04-29 (CreatePlayerS2C: added level/exp fields)
> Framework: .NET Framework 4.8 | Language: C# | Protocol: Protobuf

---

## Project Structure

```
e:\AllProject\ProtoServer\              # Workspace root
├── CodeMap.md                          # This file
├── ProtoServer.sln                     # Solution file
├── .vscode/
│   ├── tasks.json                      # MSBuild tasks (build/release/clean)
│   └── launch.json                     # Debug launcher (clr, x64)
├── packages/                           # NuGet packages
└── ProtoServer/                        # C# project
    ├── Program.cs                      # Entry point
    ├── ProtoServer.csproj              # Project file (.NET Framework 4.8, x64 Debug)
    ├── Data/
    │   ├── IDataStore.cs               # Data storage interface
    │   ├── JsonDataStore.cs            # JSON file implementation
    │   └── PlayerData.cs               # Player data model
    ├── Network/
    │   ├── EMessageType.cs             # Message type enum
    │   ├── TCPServer.cs                # TCP listener, connection lifecycle
    │   ├── ClientHandler.cs            # Per-client read/write/process
    │   └── ClientManager.cs            # Client registry & routing
    └── ProtoScripts/
        └── Generated/
            ├── NetworkModule.cs        # Protobuf generated: connect/login/heartbeat
            └── Chat.cs                 # Protobuf generated: chat messages
```

---

## Entry Point

### `ProtoServer/Program.cs`
```
namespace ProtoServer
class Program
  └── static async Task Main(string[] args)
        TCPServer(IPAddress.Any, port: 5060)
        await server.StartAsync()
```

---

## Network Layer

### `Network/EMessageType.cs` — Message Type Enum

| Value | Name | Direction | Description |
|-------|------|-----------|-------------|
| 1001 | `CONNECT_S2C` | S→C | Connection established ACK |
| 1002 | `Disconnect` | C→S | Client disconnect request |
| 2001 | `LOGIN_C2S` | C→S | Login request |
| 2002 | `LOGIN_S2C` | S→C | Login response |
| 3001 | `HEARTBEAT_C2S` | C→S | Heartbeat ping |
| 3002 | `HEARTBEAT_S2C` | S→C | Heartbeat pong |
| 4001 | `CHAT_S2C` | S→C | Chat message from server |
| 4002 | `CHAT_C2S` | C→S | Chat message from client |
| 5001 | `REGISTER_C2S` | C→S | Register request |
| 5002 | `REGISTER_S2C` | S→C | Register response |
| 6001 | `CREATE_PLAYER_C2S` | C→S | Create player request |
| 6002 | `CREATE_PLAYER_S2C` | S→C | Create player response |

---

### `Network/TCPServer.cs` — TCP Server

**Implements:** `IDisposable`, `IAsyncDisposable`

| Member | Type | Description |
|--------|------|-------------|
| `ClientManager` | `ClientManager` (public) | Client registry, exposed for external use |
| `_maxConnections` | `int` (default: 100) | Max concurrent connections, rejects excess |
| `StartAsync()` | `public Task` | Starts TcpListener, enters accept loop |
| `StopAsync()` | `public Task` | Cancels token, disconnects all clients, stops listener |
| `AcceptClientsAsync()` | `private Task` | Accept loop; continues on single error, breaks on cancel/dispose |
| `HandleClientConnectionAsync()` | `private Task` | Creates ClientHandler, notifies ClientManager on connect/disconnect |
| `BroadcastMessageAsync()` | `public Task` | Delegates to `ClientManager.BroadcastAsync` |
| `SendToClientAsync()` | `public Task` | Delegates to `ClientManager.SendToClientAsync` |
| `DisposeAsync()` | `public ValueTask` | Async dispose via `IAsyncDisposable` |

**Lifecycle Flow:**
```
StartAsync()
  └── AcceptClientsAsync()  [loop]
        └── HandleClientConnectionAsync()
              ├── ClientManager.OnClientConnected(handler)
              ├── handler.StartListeningAsync()   [blocks until disconnect]
              └── finally: ClientManager.OnClientDisconnected(handler)
```

---

### `Network/ClientHandler.cs` — Per-Client Connection

**Implements:** `IDisposable`

| Member | Type | Description |
|--------|------|-------------|
| `ClientId` | `string` (GUID, readonly) | Unique connection ID, assigned on connect |
| `AccountId` | `string` (nullable) | Player account ID, set after login |
| `RemoteEndPoint` | `string` | Client IP:Port |
| `IsConnected` | `bool` | True if not disposed + TCP connected + stream readable+writable |
| `_sendLock` | `SemaphoreSlim(1,1)` | Serializes concurrent sends to prevent packet interleaving |
| `_isDisposed` | `volatile bool` | Disposed flag (thread-visible) |
| `_isDisposedInt` | `int` | Used with `Interlocked.Exchange` for atomic one-shot disconnect |
| `_lastHeartbeatTime` | `DateTime` | Updated on each heartbeat received |
| `HeartbeatTimeout` | `TimeSpan` (60s) | Threshold for forced disconnect |
| `StartListeningAsync()` | `public Task` | Sends CONNECT_S2C ACK, starts heartbeat watcher, enters read loop |
| `SendMessageAsync()` | `public Task` | Thread-safe send: acquires `_sendLock`, packs [type(4)][len(4)][body], writes to stream |
| `ProcessMessageAsync()` | `public Task` | Dispatches received message by `EMessageType` |
| `Disconnect()` | `internal void` | Atomic one-shot: closes stream, shuts down socket |
| `HeartbeatTimeoutCheckAsync()` | `private Task` | Polls every 15s, disconnects if no heartbeat for 60s |
| `ReadExactlyAsync()` | `private Task<int>` | Reads exactly N bytes from stream (loop until complete) |

**Packet Format (Big-Endian):**
```
[ 4 bytes: EMessageType (int) ] [ 4 bytes: body length (int) ] [ N bytes: Protobuf body ]
```

**Message Dispatch (ProcessMessageAsync):**
```
CONNECT_S2C   → log only
CHAT_S2C      → (not yet implemented)
HEARTBEAT_C2S → update _lastHeartbeatTime + reply HeartbeatS2C
Disconnect    → Disconnect()
LOGIN_C2S     → parse LoginMessageC2S, log accountId (password NOT logged)
default       → log unknown type
```

---

### `Network/ClientManager.cs` — Client Registry

| Member | Type | Description |
|--------|------|-------------|
| `_clientsById` | `ConcurrentDictionary<string, ClientHandler>` | ClientId → Handler, always present after TCP connect |
| `_clientsByAccount` | `ConcurrentDictionary<string, ClientHandler>` | AccountId → Handler, set after login |
| `ConnectionCount` | `int` | Total TCP connections |
| `OnlinePlayerCount` | `int` | Logged-in players count |

**Lifecycle Methods:**

| Method | Description |
|--------|-------------|
| `OnClientConnected(handler)` | Registers into `_clientsById` |
| `OnClientLoggedIn(clientId, accountId)` | Binds AccountId; kicks existing session if same account reconnects |
| `OnClientDisconnected(handler)` | Removes from both dictionaries |

**Query Methods:**

| Method | Description |
|--------|-------------|
| `GetByClientId(clientId)` | O(1) lookup by connection ID |
| `GetByAccountId(accountId)` | O(1) lookup by player account |
| `IsAccountOnline(accountId)` | Returns bool |
| `GetAllClients()` | Snapshot list of all connections |

**Send Methods:**

| Method | Description |
|--------|-------------|
| `SendToClientAsync(clientId, type, msg)` | Send to a specific TCP connection |
| `SendToAccountAsync(accountId, type, msg)` | Send to a logged-in player by account |
| `BroadcastAsync(type, msg, excludeClientId?)` | Broadcast to all connections |
| `BroadcastToPlayersAsync(type, msg, excludeAccountId?)` | Broadcast to all logged-in players only |

---

## Protobuf Messages

### `ProtoScripts/Generated/NetworkModule.cs` — Core Protocol Messages

| Class | Fields | Usage |
|-------|--------|-------|
| `MessageHeader` | (header wrapper) | General message header |
| `NetworkMessage` | (network wrapper) | General network message |
| `S2CResult` | `bool Result` | Common response result |
| `ConnectS2C` | `S2CResult Rst` | Sent on TCP connect established |
| `LoginMessageC2S` | `int64 AccountId`, `string Password` | Client login request |
| `LoginMessageS2C` | `int64 AccountId`, `S2CResult Rst`, `PlayerInfo PlayerData` | Login response |
| `PlayerInfo` | `string PlayerName`, `int32 Gender`, `int32 Job`, `int32 Level`, `int32 Exp` | Player info embedded in login response |
| `HeartbeatC2S` | `int64 AccountId` | Client heartbeat ping |
| `HeartbeatS2C` | `int64 AccountId`, `S2CResult Rst` | Server heartbeat pong |
| `RegisterC2S` | `int64 AccountId`, `string Password` | Register request |
| `RegisterS2C` | `int64 AccountId`, `S2CResult Rst` | Register response |
| `CreatePlayerC2S` | `string PlayerName`, `int32 Gender`, `int32 Job` | Create player request |
| `CreatePlayerS2C` | `S2CResult Rst`, `string PlayerName`, `int32 Level`, `int32 Exp` | Create player response |

### `ProtoScripts/Generated/Chat.cs` — Chat Messages

| Class | Fields | Usage |
|-------|--------|-------|
| `ChatC2S` | (chat content) | Client → Server chat message |
| `ChatS2C` | (chat content) | Server → Client chat message |

---

## Key Design Decisions

| Topic | Decision | Reason |
|-------|----------|--------|
| Client storage | `ConcurrentDictionary` (two indexes) | O(1) lookup; no external lock needed |
| Send thread-safety | `SemaphoreSlim(1,1)` per connection | Prevents interleaved writes on NetworkStream |
| Disconnect atomicity | `Interlocked.Exchange` on int flag | Guarantees exactly-once Disconnect execution |
| `_isDisposed` | `volatile bool` | Ensures cross-thread visibility without full lock |
| Heartbeat | Client sends C2S every N seconds; server replies S2C; 60s no heartbeat = force disconnect | Detects ghost connections |
| Max connections | Checked before creating `ClientHandler` | Prevents resource exhaustion / DDoS |
| Accept loop errors | `continue` on general exception | Single-connection failure doesn't kill accept loop |
| Async dispose | Implements `IAsyncDisposable` | Avoids fire-and-forget async in `Dispose()` |
| Password logging | Account ID logged only, password suppressed | Security: no plaintext credential leakage |

---

## Login Flow (How to complete)

```
1. Client sends LOGIN_C2S (AccountId + Password)
2. ClientHandler.ProcessMessageAsync receives LOGIN_C2S
3. Server validates credentials
4. On success:
   server.ClientManager.OnClientLoggedIn(handler.ClientId, accountId)
5. Server sends LOGIN_S2C with Result=true
6. From now on:
   server.ClientManager.SendToAccountAsync(accountId, ...)  <- works
```

---

## VS Code Build Configuration

| Task | Command |
|------|---------|
| Build (Debug) | `MSBuild.exe ProtoServer.csproj /t:Build /p:Configuration=Debug` |
| Build (Release) | `MSBuild.exe ProtoServer.csproj /t:Build /p:Configuration=Release` |
| Clean | `MSBuild.exe ProtoServer.csproj /t:Clean` |
| Debug Launch | `F5` → runs `ProtoServer/bin/Debug/ProtoServer.exe` via `clr` debugger (x64) |

> MSBuild path: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`

---

## Data Layer

### `Data/IDataStore.cs` — Storage Interface

| Method | Returns | Description |
|--------|---------|-------------|
| `CreateAccountAsync(accountId, password)` | `Task<bool>` | Register new account. false if already exists. |
| `ValidateLoginAsync(accountId, password)` | `Task<bool>` | Verify credentials. |
| `AccountExistsAsync(accountId)` | `Task<bool>` | Check if account registered. |
| `GetPlayerDataAsync(accountId)` | `Task<PlayerData>` | Get player game data. Returns default if new. |
| `SavePlayerDataAsync(accountId, data)` | `Task` | Persist player data. |

### `Data/JsonDataStore.cs` — JSON File Implementation

**File structure on disk:**
```
bin/Debug/Data/
├── accounts.json          # { "accountId": "sha256hash", ... }
└── players/
    ├── player001.json     # PlayerData JSON per account
    └── player002.json
```

**Key design:**
- Accounts loaded into `ConcurrentDictionary` at startup for O(1) lookup
- Passwords stored as SHA256 hash, never plaintext
- Player data: one JSON file per account, read/write on demand
- Thread-safe: `ConcurrentDictionary` for reads, `lock` for accounts file writes

### `Data/PlayerData.cs` — Player Data Model

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Level` | `int` | 1 | Player level |
| `Gold` | `int` | 0 | Currency |
| `Exp` | `int` | 0 | Experience points |
| `Equipment` | `List<string>` | `[]` | Equipment item IDs |
| `LastLoginTime` | `string` | — | ISO 8601 timestamp |
| `CreatedTime` | `string` | — | ISO 8601 timestamp |

### Usage in Code

```csharp
// Access via TCPServer
server.DataStore.CreateAccountAsync("player001", "mypassword");
server.DataStore.ValidateLoginAsync("player001", "mypassword");

var data = await server.DataStore.GetPlayerDataAsync("player001");
data.Gold += 100;
await server.DataStore.SavePlayerDataAsync("player001", data);
```

### Future Migration Path

```
IDataStore (interface)
├── JsonDataStore     ← current, local dev
├── SqliteDataStore   ← future: same interface, swap in Program.cs
└── MySqlDataStore    ← future: production, same interface
```

To switch storage: change one line in `Program.cs`:
```csharp
// IDataStore dataStore = new JsonDataStore();       // current
// IDataStore dataStore = new SqliteDataStore();     // future
// IDataStore dataStore = new MySqlDataStore(connStr); // production
```

---

## Proto Protocol Definitions

> Source: `ProtoFiles/` — the single source of truth for all message definitions.
> Generated C# code: `ProtoScripts/Generated/` (via `protoc`).

### `ProtoFiles/network_module.proto` — Core Protocol

| Message | Fields | Description |
|---------|--------|-------------|
| `MessageHeader` | `int32 message_id = 1` | Message ID wrapper |
| `NetworkMessage` | `MessageHeader messageHeader = 2` | Network message wrapper |
| `S2CResult` | `bool result = 1`, `int32 errCode = 2` | Common server response |
| `ConnectS2C` | `S2CResult rst = 1` | Connection ACK |
| `LoginMessageC2S` | `int64 accountId = 1`, `string password = 2` | Login request |
| `LoginMessageS2C` | `int64 accountId = 1`, `S2CResult rst = 2`, `PlayerInfo playerData = 3` | Login response |
| `PlayerInfo` | `string playerName = 1`, `int32 gender = 2`, `int32 job = 3`, `int32 level = 4`, `int32 exp = 5` | Player info |
| `HeartbeatC2S` | `int64 accountId = 1` | Heartbeat ping |
| `HeartbeatS2C` | `int64 accountId = 1`, `S2CResult rst = 2` | Heartbeat pong |
| `RegisterC2S` | `int64 accountId = 1`, `string password = 2` | Register request |
| `RegisterS2C` | `int64 accountId = 1`, `S2CResult rst = 2` | Register response |

### `ProtoFiles/player.proto` — Player Protocol

| Message | Fields | Description |
|---------|--------|-------------|
| `CreatePlayerC2S` | `string playerName = 1`, `int32 gender = 2`, `int32 job = 3` | Create player request |
| `CreatePlayerS2C` | `S2CResult rst = 1`, `string playerName = 2`, `int32 level = 3`, `int32 exp = 4` | Create player response |

| Message | Fields | Description |
|---------|--------|-------------|
| `ChatC2S` | (empty) | Client chat message |
| `ChatS2C` | `S2CResult result = 1` | Server chat response |

> **IMPORTANT:** `chat.proto` imports `network_module.proto` for `S2CResult`.

---

## ProtoTools — Proto Build Toolchain

> Location: `ProtoTools/`

### `compile_proto.bat` — Compile All Proto Files

Compiles all `.proto` files from `ProtoFiles/` to C# using `protoc`.

| Setting | Value |
|---------|-------|
| Input | `E:\AllProject\ProtoFiles` |
| Output 1 | `E:\AllProject\PureMVC_Framework\Assets\Scripts\ProtoScripts\Generated` (Unity client) |
| Output 2 | `E:\AllProject\ProtoServer\ProtoServer\ProtoScripts\Generated` (C# server) |
| Requires | `protoc` in PATH |

**Flow:**
```
1. Check protoc exists
2. Create output dirs if missing
3. Clean both output dirs
4. For each .proto file:
     protoc --csharp_out=<output> <file>
5. Done
```

### `create_proto.bat` — Create New Proto File

Interactive script to scaffold a new `.proto` file with C2S/S2C message templates.

**Template generated:**
```protobuf
syntax = "proto3";
import "network_module.proto";
message <Name>C2S { }
message <Name>S2C { S2CResult result = 1; }
```

### `clear_all_proto.bat` — Clean All Generated Files

Deletes all files from:
- `ProtoFiles/`
- `PureMVC_Framework/Assets/Scripts/ProtoScripts/Generated/`
- `ProtoServer/ProtoServer/ProtoScripts/Generated/`

---

## ⚠️ Known Issues

### Proto vs Generated Code Mismatch — ✅ FIXED (2026-04-28)

Regenerated C# from `.proto` files via `protoc`. All `accountId` fields now use `int` consistently across proto definitions, generated code, and server logic.

**Files updated:**
- `ProtoScripts/Generated/NetworkModule.cs` — regenerated
- `ProtoScripts/Generated/Chat.cs` — regenerated
- `Network/ClientHandler.cs` — `AccountId` changed from `string` to `int`
- `Network/ClientManager.cs` — `_clientsByAccount` changed to `ConcurrentDictionary<int, ClientHandler>`
- `Data/IDataStore.cs` — all `accountId` params changed to `int`
- `Data/JsonDataStore.cs` — `_accounts` changed to `ConcurrentDictionary<int, string>`

---

## Register Flow

```
1. Client sends REGISTER_C2S (accountId + password)
2. ClientHandler.ProcessMessageAsync receives REGISTER_C2S
3. Calls server.DataStore.CreateAccountAsync(accountId, password)
4. JsonDataStore checks if accountId already exists in _accounts dict
5. If duplicate → returns false
6. If new → SHA256 hashes password, saves to accounts.json, returns true
7. Server sends REGISTER_S2C:
     Result = true,  ErrCode = 0   (success)
     Result = false, ErrCode = 1   (account already exists)
```
