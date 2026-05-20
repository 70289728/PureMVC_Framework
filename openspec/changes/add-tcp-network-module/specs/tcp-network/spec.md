## ADDED Requirements

### Requirement: TCP connection management
The system SHALL provide a `NetworkManager` singleton (MonoBehaviour) that manages the lifecycle of a single TCP connection to a configurable host and port.

#### Scenario: Successful connection
- **WHEN** `NetworkManager.Connect(host, port)` is called
- **THEN** a TCP socket is established to the specified endpoint and a `NETWORK_CONNECTED` notification is sent

#### Scenario: Connection failure
- **WHEN** `NetworkManager.Connect` is called but the server is unreachable
- **THEN** a `NETWORK_ERROR` notification is sent with the error message as body

#### Scenario: Disconnection
- **WHEN** `NetworkManager.Disconnect()` is called or the socket closes unexpectedly
- **THEN** the background receive thread is stopped and a `NETWORK_DISCONNECTED` notification is sent

---

### Requirement: Packet framing and reassembly
The system SHALL frame outgoing and incoming messages using the format `[4-byte msgId (big-endian int32)][4-byte bodyLength (big-endian int32)][body bytes]` and SHALL correctly reassemble split or combined (sticky) packets.

#### Scenario: Send a framed packet
- **WHEN** `NetworkManager.Send(msgId, protoMessage)` is called
- **THEN** the packet `[msgId][bodyLength][body]` is enqueued and written to the socket

#### Scenario: Receive a split packet
- **WHEN** TCP data arrives in multiple partial reads that together form one complete packet
- **THEN** `PacketHandler` buffers the data and only yields the complete packet once all bytes are available

#### Scenario: Receive combined packets
- **WHEN** TCP data arrives containing bytes for more than one complete packet
- **THEN** `PacketHandler` yields each complete packet separately in order

---

### Requirement: Message dispatch
The system SHALL route incoming packets to registered callbacks by message ID.

#### Scenario: Register and receive callback
- **WHEN** a listener calls `NetworkManager.Dispatcher.Register(msgId, callback)` and a packet with that `msgId` is received
- **THEN** `callback(bodyBytes)` is invoked on the Unity main thread

#### Scenario: Unregister callback
- **WHEN** `NetworkManager.Dispatcher.Unregister(msgId, callback)` is called
- **THEN** that callback no longer receives packets for that message ID

#### Scenario: No handler registered
- **WHEN** a packet arrives with a `msgId` that has no registered callback
- **THEN** the packet is discarded and a warning is logged via `Log.w`

---

### Requirement: Receive loop on background thread
The system SHALL run the TCP receive loop on a dedicated background thread and dispatch decoded packets to the Unity main thread via a thread-safe queue.

#### Scenario: Non-blocking main thread
- **WHEN** the TCP receive loop is waiting for data
- **THEN** the Unity main thread continues to run at normal frame rate without any blocking

#### Scenario: Main-thread dispatch
- **WHEN** complete packets are dequeued in `NetworkManager.Update()`
- **THEN** `MessageDispatcher.Dispatch` is called on the Unity main thread

---

### Requirement: NetworkManager initialization
The system SHALL initialize `NetworkManager` during game startup alongside other managers.

#### Scenario: Initialized in GameMain
- **WHEN** `GameMain.InitManagers()` is called
- **THEN** `NetworkManager.Instance` is accessed to ensure the singleton is created
