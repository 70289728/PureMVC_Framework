## 1. Dependencies & Project Setup

- [ ] 1.1 Add Google.Protobuf runtime to the Unity project (via `.unitypackage` or NuGet for Unity)
- [x] 1.2 Create directory `Assets/Scripts/Network/` and `Assets/Scripts/Network/Proto/`

## 2. Constants

- [x] 2.1 Create `Assets/Scripts/Const/MessageConst.cs` — integer message ID constants (placeholder entries)
- [x] 2.2 Create `Assets/Scripts/Network/NetworkNotificationConst.cs` — PureMVC notification constants: `NETWORK_CONNECTED`, `NETWORK_DISCONNECTED`, `NETWORK_ERROR`

## 3. PacketHandler

- [x] 3.1 Create `Assets/Scripts/Network/PacketHandler.cs`
  - Internal `List<byte>` receive buffer
  - `Append(byte[] data, int length)` — appends raw bytes to buffer
  - `TryReadPacket(out int msgId, out byte[] body) : bool` — reads header (8 bytes: 4 msgId + 4 length), returns body if enough bytes available; removes consumed bytes from buffer
  - Big-endian byte order for header fields

## 4. MessageDispatcher

- [x] 4.1 Create `Assets/Scripts/Network/MessageDispatcher.cs`
  - `Dictionary<int, List<Action<byte[]>>>` handler map
  - `Register(int msgId, Action<byte[]> handler)`
  - `Unregister(int msgId, Action<byte[]> handler)`
  - `Dispatch(int msgId, byte[] body)` — invokes all registered handlers; logs warning via `Log.w` if no handler found

## 5. NetworkManager

- [x] 5.1 Create `Assets/Scripts/Manager/NetworkManager.cs` as a MonoBehaviour singleton (following the pattern of other managers)
- [x] 5.2 Expose `public MessageDispatcher Dispatcher` property
- [x] 5.3 Implement `Connect(string host, int port)` — creates `TcpClient`, connects, starts background receive thread, sends `NETWORK_CONNECTED` notification on success; sends `NETWORK_ERROR` on failure
- [x] 5.4 Implement background receive thread loop — reads from `NetworkStream`, calls `PacketHandler.Append`, then drains `TryReadPacket` and enqueues decoded packets to `ConcurrentQueue<(int msgId, byte[] body)>`
- [x] 5.5 Implement `Update()` — drains `ConcurrentQueue` each frame, calls `Dispatcher.Dispatch` for each packet on the main thread
- [x] 5.6 Implement `Send(int msgId, byte[] body)` — builds frame `[msgId(4)][bodyLen(4)][body]`, enqueues to outgoing `ConcurrentQueue<byte[]>`
- [x] 5.7 Implement `Send<T>(int msgId, T proto) where T : Google.Protobuf.IMessage` — serializes proto to bytes, calls `Send(msgId, bytes)`
- [x] 5.8 Implement background send thread loop — drains outgoing queue and writes bytes to `NetworkStream`
- [x] 5.9 Implement `Disconnect()` — signals both threads to stop, closes socket, sends `NETWORK_DISCONNECTED` notification
- [x] 5.10 Implement `OnDestroy()` — calls `Disconnect()` to clean up on scene unload / app quit

## 6. GameMain Integration

- [x] 6.1 Add `NetworkManager.Instance` access in `GameMain.InitManagers()` to ensure the singleton is created on startup

## 7. CodeMap Update

- [x] 7.1 Update `Assets/.codemaker/CodeMap.md` to document `NetworkManager`, `PacketHandler`, `MessageDispatcher`, `NetworkNotificationConst`, and `MessageConst` in the Manager / Network section
