// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 3: P2P & Advanced Features
// Due: Week 14
//
// NOTE: This file is NOT used in Sprint 1 or Sprint 2!
//
// THE TRANSPORT LAYER DOESN'T CHANGE FOR SPRINT 3 - READ THIS FIRST
//
// In Sprint 1-2, Server.cs and Client.cs track raw connections by endpoint
// string ("192.168.1.5:54321") or TcpClient. That's the client/server model:
// one Server relaying between many clients, one Client talking to it.
//
// Sprint 3 does NOT require you to change Server.cs or Client.cs at all.
// They're already exactly what P2P needs at the transport level: Server
// accepts many incoming connections (now from other peers instead of chat
// clients), and Client makes an outgoing connection (now to a peer instead
// of a server). The only change is usage - instead of one Client instance
// talking to one server, your node creates ONE Client PER outgoing peer
// connection, and keeps them in a dictionary keyed by peer id, right next
// to the Server that's still accepting incoming ones. Both together make
// your node "both a server and a client at once," which is what P2P means.
//
// Peer is where the NEW state Sprint 3 needs actually lives - AES session
// key, reconnection attempts, last-seen timestamp, whether a connection
// came in or went out. This class wraps a Server-tracked endpoint or a
// Client instance with that metadata; it does not replace either of them.
// You'll maintain a Dictionary<string, Peer> in Program.cs (keyed by peer
// id from PeerDiscovery), and look up the right endpoint or Client instance
// through it whenever you need to actually send something.
//

using System.Net;
using System.Net.Sockets;

namespace SecureMessenger.Core;

/// <summary>
///     Represents a connected peer in the network.
///     Sprint 3 introduces the "peer" concept for true P2P networking:
///     - Each peer can both send and receive messages
///     - Peers are discovered automatically via UDP broadcast
///     - Connections are monitored with heartbeats
///     - Disconnected peers trigger automatic reconnection attempts
///     This replaces the simple client/server model from Sprint 1-2
///     with a more sophisticated peer-to-peer architecture.
/// </summary>
public class Peer
{
	/// <summary>Unique identifier for this peer (first 8 chars of GUID)</summary>
	public string Id { get; set; } = Guid.NewGuid().ToString()[..8];

	/// <summary>Display name of the peer</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>IP address of the peer</summary>
	public IPAddress? Address { get; set; }

	/// <summary>TCP port the peer is listening on</summary>
	public int Port { get; set; }

	/// <summary>Last time we received data from this peer (for heartbeat timeout)</summary>
	public DateTime LastSeen { get; set; } = DateTime.Now;

	/// <summary>Whether we currently have an active connection to this peer</summary>
	public bool IsConnected { get; set; }

	// Network connection handles
	public TcpClient? Client { get; set; }
	public NetworkStream? Stream { get; set; }

	// Convenience wrappers for text-based messaging
	// These simplify reading/writing JSON lines over the network
	public StreamReader? Reader { get; set; }
	public StreamWriter? Writer { get; set; }

	// Sprint 2 security: Per-peer encryption keys
	// These are negotiated during the key exchange handshake
	public byte[]? AesKey { get; set; }
	public byte[]? PublicKey { get; set; }

	// Sprint 3: Reconnection tracking
	public int ReconnectAttempts { get; set; }
	public DateTime? LastReconnectAttempt { get; set; }

	public override string ToString()
	{
		var status = IsConnected ? "Connected" : "Disconnected";
		return $"{Name} ({Address}:{Port}) - {status}";
	}

	/// <summary>
	///     Clean up all resources associated with this peer connection.
	///     Call this when disconnecting a peer.
	/// </summary>
	public void Dispose()
	{
		IsConnected = false;
		try
		{
			Reader?.Dispose();
		}
		catch { }

		try
		{
			Writer?.Dispose();
		}
		catch { }

		try
		{
			Stream?.Dispose();
		}
		catch { }

		try
		{
			Client?.Dispose();
		}
		catch { }
	}
}