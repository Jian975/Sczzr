# Secure Distributed Messenger

CSCI 251: Concepts of Parallel and Distributed Systems

> For sprint submissions, use the `sprint-X-documentation.md` templates in the templates folder. This README is just for
> your GitHub repo.

## Build & Run

Requires .NET 9.0 SDK or later.

```bash
dotnet build
dotnet run
```

## Commands

- `/connect <ip> <port>` - Connect to a server
- `/listen <port>` - Start listening for connections
- `/peers` - Show connection status
- `/history` - View message history (Sprint 3)
- `/quit` - Exit

### Quick Test

Open three terminals in the project directory:

```
# Terminal 1 (server)
dotnet run
/listen 5000

# Terminal 2 (client)
dotnet run
/connect 127.0.0.1 5000

# Terminal 3 (client)
dotnet run
/connect 127.0.0.1 5000
```

Terminals 2 and 3 can now chat. Terminal 1 just relays.

## Project Structure

```
SecureMessenger/
├── Program.cs                 # Entry point, main loop, event wiring
├── Core/
│   ├── Message.cs             # Message model (provided)
│   ├── MessageQueue.cs        # Thread-safe queue (optional for Sprint 1)
│   ├── Peer.cs                # Live connection + peer info (Sprint 3, provided)
│   └── PeerInfo.cs            # Lightweight, serializable peer info for the wire protocol (Sprint 3, provided)
├── Network/
│   ├── Server.cs              # TCP listener, accepts connections
│   ├── Client.cs              # TCP client, connects to servers
│   ├── PeerDiscovery.cs       # Bootstrap + peer-exchange mesh formation (Sprint 3; required) — UDP LAN broadcast is also in here but optional/not graded
│   ├── HeartbeatMonitor.cs    # Connection monitoring (Sprint 3)
│   └── ReconnectionPolicy.cs  # Auto-reconnect (Sprint 3)
├── Security/
│   ├── AesEncryption.cs       # AES encrypt/decrypt (Sprint 2)
│   ├── RsaEncryption.cs       # RSA keys (Sprint 2)
│   ├── MessageSigner.cs       # Signatures (Sprint 2)
│   └── KeyExchange.cs         # Key exchange (Sprint 2)
└── UI/
    ├── ConsoleUI.cs           # Command parsing, message display
    └── MessageHistory.cs      # Message persistence (Sprint 3)
```

## What to Implement

Every method that throws `NotImplementedException` needs your code. The TODO comments in each method explain what to do.

`Message.cs`, `Peer.cs`, and `PeerInfo.cs` are already complete - don't modify those.

## Getting Started

If you're new to C# networking or threading, read `HINTS.md` first. It covers events, TCP sockets, threading, and how
the pieces fit together.

### Sprint 1: Threading & Networking (Week 7)

Files: `Program.cs`, `Network/Server.cs`, `Network/Client.cs`, `UI/ConsoleUI.cs`

Get the basic client/server working: server listens and relays messages, clients connect and chat.

### Sprint 2: Security (Week 10)

Files: everything in `Security/`

Add encryption on top of your Sprint 1 networking. Messages get encrypted before sending and decrypted after receiving.
Key exchange happens when clients connect.

### Sprint 3: P2P (Week 14)

Files: `Network/PeerDiscovery.cs`, `Network/HeartbeatMonitor.cs`, `Network/ReconnectionPolicy.cs`,
`UI/MessageHistory.cs`

Move from client/server to true peer-to-peer. The required discovery mechanism is bootstrap + peer exchange: `/connect`
to one known peer, then trade peer lists over that TCP connection so the mesh grows on its own (see the comment block at
the top of `PeerDiscovery.cs` for why — short version: UDP broadcast doesn't survive Docker/VPNs/subnets, so it's
optional and ungraded, not the required path). Add heartbeats, reconnection, and message history on top of that.
