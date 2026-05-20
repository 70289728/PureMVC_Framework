## Context

The project is a Unity game built on the PureMVC framework. It currently has no network layer. We need a TCP client that can connect to a local development server (127.0.0.1:8888), send and receive Protobuf-encoded messages, and integrate cleanly with the existing PureMVC notification/manager pattern.

The Google.Protobuf runtime package must be added to the project.

## Goals / Non-Goals

**Goals:**
- Async TCP client running on a background thread (receive loop) to avoid blocking the main thread
- Packet framing: `[4-byte msgId][4-byte bodyLength][body bytes]`
- Sticky-packet and split-packet handling via a receive buffer
- Message dispatch: incoming message ID maps to registered `Action<byte[]>` callbacks
- PureMVC notifications for connection events (connected, disconnected, error)
- `NetworkManager` singleton initialized in `GameMain` alongside other managers
- Configurable host/port (defaulting to 127.0.0.1:8888)

**Non-Goals:**
- Heartbeat / keepalive (deferred)
- TLS / encryption (deferred)
- UDP support (deferred)
- Server-side code (handled separately in a VS project)
- Generating `.proto` files (provided by the user)

## Decisions

### D1 — Async receive on background thread, dispatch on main thread
**Decision:** The TCP receive loop runs on a `Thread` (not a coroutine). Received and fully framed packets are queued to a `ConcurrentQueue<(int msgId, byte[] body)>`. `NetworkManager.Update()` drains the queue each frame and dispatches on the Unity main thread.

**Rationale:** Coroutines block on yield; a background thread is more responsive for streaming data. Dispatching on the main thread avoids Unity API threading issues and keeps PureMVC notification calls safe.

**Alternatives considered:**
- Pure coroutine polling: simpler but higher latency and potential frame drops.
- Task/async-await: viable but adds complexity around Unity synchronization context.

### D2 — Packet framing in PacketHandler (separate from NetworkManager)
**Decision:** `PacketHandler` owns the byte buffer, `Append(byte[])`, and `TryReadPacket(out int msgId, out byte[] body)`. `NetworkManager` calls into it.

**Rationale:** Single-responsibility. `PacketHandler` can be unit-tested without a socket.

### D3 — MessageDispatcher as a plain C# class (not MonoBehaviour)
**Decision:** `MessageDispatcher` is a standalone class held by `NetworkManager`. It exposes `Register(int msgId, Action<byte[]>)` / `Unregister` / `Dispatch(int msgId, byte[])`.

**Rationale:** No need for GameObject lifecycle. Keeps the dispatcher lightweight and testable.

### D4 — Protobuf send helper on NetworkManager
**Decision:** `NetworkManager.Send<T>(int msgId, T proto) where T : IMessage` serializes the proto to bytes, prepends the header, and enqueues the bytes for the send thread.

**Rationale:** Callers should not need to know about byte layout. Centralizing serialization in one place makes future format changes easy.

### D5 — Integration with PureMVC via Notifications
**Decision:** On Connected, Disconnected, and NetworkError events, `NetworkManager` fires `SendNotification` using constants from `NetworkNotificationConst`. Individual message callbacks are handled through `MessageDispatcher` (not notifications), to avoid flooding the PureMVC observer system.

**Rationale:** Connection state is global and relevant to many systems (use notifications). Per-message routing is high-frequency and targeted (use callbacks).

## Risks / Trade-offs

- **Thread safety of send queue** → Use `ConcurrentQueue<byte[]>` for outgoing packets; send on the background thread.
- **Google.Protobuf not present in project** → Must be added manually via `.unitypackage` or NuGet for Unity before compilation succeeds.
- **No heartbeat** → Connection may silently drop; reconnection logic is manual. Acceptable for local dev phase.
- **Large messages** → No chunking beyond TCP framing; 4-byte length header caps messages at ~2 GB (sufficient).

## Open Questions

- None blocking implementation. Heartbeat interval and reconnect strategy TBD when moving to production.
