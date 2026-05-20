# PureMVC Framework

Unity + PureMVC + HybridCLR hot-update game framework with C# Protobuf TCP server.

## Overview

A full-stack game development framework built on PureMVC architecture with HybridCLR hot-update support. Features a complete client-server system using Protobuf over TCP.

| Layer | Tech Stack |
|-------|------------|
| Client | Unity 2022.3.62f2 + PureMVC + HybridCLR |
| Server | .NET Framework 4.8 + Protobuf + TCP |
| Protocol | Google Protobuf |

## Architecture

```
┌─────────────────────────────────────────────┐
│  AOTAssembly (GameMain.cs)                  │
│  Scene entry · Reflection bridge            │
└──────────────────┬──────────────────────────┘
                   │ reflection
       ┌───────────┼──────────────────────┐
       ▼           ▼                      ▼
┌──────────────┐ ┌────────────────────────────┐
│Framework     │ │  HotUpdateAssembly         │
│Assembly (AOT)│ │  (Hot-updatable)           │
│              │ │                            │
│· PureMVC Core│ │· LoginSuccessCommand       │
│· All Managers│ │· HotUpdate Startup         │
│· All Commands│ │· GameConfigCs              │
│· Network     │ │· HotUpdateProtoScripts     │
│· HotUpdate   │ │· UI Main/Shop/Bag          │
│· UI Login/   │ └────────────────────────────┘
│  HotUpdate   │
│· All Proxies │
│· Common/Log  │
│· Const       │
│· CustomBase  │
│· UIComponent │
└──────────────┘

┌─────────────────────────────────────────────┐
│  ProtoServer (TCP, Port 5060)               │
│  · TCPServer · ClientHandler · JsonDataStore│
│  · Protobuf: Chat/Shop/Bag/Mail/SignIn etc. │
└─────────────────────────────────────────────┘
```

## Key Features

### Client
- **PureMVC Pattern** — Clear separation: Proxy (data) / Mediator (view) / Command (logic)
- **HybridCLR Hot Update** — C# hot-update without Lua overhead. Download new DLL → restart → active
- **Assembly Isolation** — FrameworkAssembly (AOT, stable) / HotUpdateAssembly (hot-updatable)
- **UI Binding System** — Component-level data binding (TextBind, ButtonBind, ImageBind...)
- **Network Layer** — Protobuf serialization + TCP with message dispatch
- **Asset Management** — AssetBundle loading with hot-update support
- **Common Utilities** — Log system, Timer, ObjectPool, CoroutineRunner, UpdateManager

### Server
- **TCP Socket Server** — Async TCP listener with per-client handler
- **Protobuf Protocol** — Strongly-typed messages for all game modules
- **JSON Data Storage** — Player data persisted as JSON files
- **Module Coverage** — Login, Chat, Shop, Bag, Mail, Friend, Announce, SignIn

## Project Structure

```
PureMVC_Framework/
├── Assets/
│   ├── Scripts/
│   │   ├── AOTAssembly/           # AOT entry point (GameMain.cs)
│   │   ├── FrameworkAssembly/     # Stable code (AOT)
│   │   │   ├── PureMVCFramework/  # PureMVC Core
│   │   │   ├── Manager/           # Singleton managers
│   │   │   ├── Command/           # Business commands
│   │   │   ├── Proxy/             # Data models
│   │   │   ├── CustomBase/        # Base classes
│   │   │   ├── Network/           # TCP client + Protobuf
│   │   │   ├── HotUpdate/         # Hot-update downloader
│   │   │   ├── UIComponent/       # UI binding system
│   │   │   ├── Common/            # Log, Timer, helpers
│   │   │   ├── Const/             # Constants
│   │   │   ├── BaseProtoScripts/  # Shared protobuf code
│   │   │   └── GameConfigCs/      # Config table models
│   │   └── HotUpdateAssembly/     # Hot-updatable code
│   │       ├── Command/           # Login flow etc.
│   │       ├── Const/             # Constants
│   │       ├── GameConfigCs/      # Config models
│   │       ├── HotUpdateProtoScripts/  # Protobuf code
│   │       └── Network/           # Network helpers
│   └── ... (resources, scenes, etc.)
├── ProtoServer/                   # C# server project
│   ├── ProtoServer/               # Main project
│   │   ├── Network/               # TCPServer + ClientHandler
│   │   ├── Data/                  # JsonDataStore
│   │   └── ProtoScripts/Generated/ # Protobuf generated code
│   ├── ProtoFiles/                # .proto source files
│   ├── ProtoTools/                # Code generation tools
│   └── ProtoServer.sln            # Solution file
├── local_server/                  # Python test server
├── HybridCLRData/                 # HybridCLR configuration
├── Packages/                      # Unity packages
├── ProjectSettings/               # Unity settings
└── .gitignore
```

## Quick Start

### Prerequisites
- Unity 2022.3.62f2
- .NET Framework 4.8 (for server)
- Git

### Client (Unity)
1. Open the project in Unity Hub (2022.3.62f2)
2. Wait for package import to complete
3. Open `Assets/Scenes/MainScene`
4. Press Play

### Server
```bash
# Option 1: C# Server
cd ProtoServer
# Open ProtoServer.sln with Visual Studio / Rider
# Build & Run (Debug, x64)

# Option 2: Python Test Server
cd local_server
python server.py
```

### Hot Update
```
1. Build HotUpdateAssembly.dll → upload to server
2. Client downloads new DLL to persistentDataPath/cache/
3. Restart app → new code active
```

## Managers

| Manager | Description |
|---------|-------------|
| `UIManager` | UI panel lifecycle, layer management |
| `NetworkManager` | TCP connection, send/receive |
| `TimerManager` | Timed task scheduling |
| `AssetBundleManager` | AssetBundle load/cache |
| `AssetManager` | Asset loading abstraction |
| `AudioManager` | BGM/SFX playback |
| `UpdateManager` | MonoBehaviour Update/Coroutine manager |
| `ObjectPoolManager` | Generic object pool |
| `SaveManager` | Local data persistence |
| `ConfigManager` | Configuration table loading |
| `PlayerPrefsManager` | PlayerPrefs wrapper |
| `GameSceneManager` | Scene loading/unloading |

## PureMVC Binding

Standard PureMVC flow in this project:

```
User Action → Mediator → SendNotification(name, body)
    → Command.Execute(notification)
        → Proxy logic (network request / data update)
            → SendNotification(result)
                → Mediator.HandleNotification → Update UI
```

All Commands, Proxies and Mediators auto-register via reflection at startup.

## License

MIT
