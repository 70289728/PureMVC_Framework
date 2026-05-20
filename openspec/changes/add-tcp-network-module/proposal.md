## Why

The project currently has no networking layer. A TCP network module is needed to enable client-server communication, starting with a local server for development and testing, with the intention of switching to a production server in the future.

## What Changes

- Add `NetworkManager` (MonoBehaviour Singleton) — manages TCP connection lifecycle (connect, disconnect, reconnect)
- Add `PacketHandler` — handles send/receive, framing, and sticky-packet/split-packet reassembly
- Add `MessageDispatcher` — routes incoming messages by message ID to registered callbacks
- Add `MessageConst` — integer constants for message IDs
- Add `NetworkNotificationConst` — PureMVC notification constants for network events (connected, disconnected, error)
- Add a `Proto/` directory placeholder for generated Protobuf C# files
- Update `GameMain.cs` to initialize `NetworkManager`
- Update `CodeMap.md` to document the new network layer

**Packet format:** `[4-byte Message ID (int32)][4-byte Body Length (int32)][Protobuf body bytes]`

## Capabilities

### New Capabilities

- `tcp-network`: TCP client with connect/disconnect, packet framing (msgId + length + body), Protobuf serialization, message dispatching integrated with PureMVC notifications

### Modified Capabilities

- `game-main`: `GameMain.cs` gains `NetworkManager` initialization

## Impact

- New dependency: `Google.Protobuf` (Unity NuGet / .unitypackage)
- New directory: `Assets/Scripts/Network/`
- `GameMain.cs`: one additional manager initialization call
- No breaking changes to existing PureMVC flow
