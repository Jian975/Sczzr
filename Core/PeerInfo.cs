// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger
//
// PROVIDED - No implementation required
//
// SPRINT 3: P2P & Advanced Features
//
// PeerInfo is the lightweight, JSON-serializable version of a peer, used
// only for the wire protocol (the PeerList message in PeerDiscovery.cs).
// The full Peer class in Peer.cs also holds live connection objects
// (TcpClient, NetworkStream, StreamReader/Writer) that cannot be
// serialized to JSON and should never be sent to another machine anyway —
// you don't send your socket, you send enough information for the other
// side to open their own connection.
//

namespace SecureMessenger.Core;

/// <summary>
///     Just enough information to find and connect to a peer: its id, address,
///     and TCP listen port. Used for the PeerList message exchanged during
///     peer discovery (Sprint 3). Not the same thing as Peer, which also holds
///     the live socket/stream for a connection you already have.
/// </summary>
public class PeerInfo
{
	public string Id { get; set; } = string.Empty;
	public string Address { get; set; } = string.Empty;
	public int Port { get; set; }

	/// <summary>Build a PeerInfo from a connected Peer, for sending to others.</summary>
	public static PeerInfo FromPeer(Peer peer)
	{
		return new PeerInfo
		{
			Id = peer.Id,
			Address = peer.Address?.ToString() ?? string.Empty,
			Port = peer.Port
		};
	}
}