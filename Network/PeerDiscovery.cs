// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 3: P2P & Advanced Features
// Due: Week 14
//
// NOTE: This file is NOT used in Sprint 1 or Sprint 2!
//
// PEER DISCOVERY — READ THIS FIRST
//
// In Sprint 1-2 there was one server, and clients found it with /connect.
// In Sprint 3 there's no server at all — every node is a peer, and the
// mesh has to form itself. This class gives you two ways to build it:
//
//   1. REQUIRED — Bootstrap + Peer Exchange (over TCP). You connect to ONE
//      known peer with /connect (same command you already have from
//      Sprint 1). Once connected, the two of you exchange your peer
//      lists (BuildPeerListMessage / ProcessPeerListMessage below), so
//      each side learns who the other already knows — and connects to
//      any new names it hears. Repeat that a couple of hops and a single
//      bootstrap connection grows into a full mesh on its own. This is
//      what the grader tests, and it's what your demo video should show.
//
//   2. OPTIONAL, NOT GRADED — LAN broadcast auto-discovery (BroadcastLoop /
//      ListenLoop below). Shouting "I'm here!" to everyone on the local
//      network is a fun trick on a real LAN, but it does not reliably
//      cross subnets, VPNs, or container networks — Docker bridge
//      networks in particular do not propagate broadcast traffic between
//      containers, which is exactly why this isn't part of #1 and isn't
//      graded. Implement it if you want, purely for your own local
//      testing convenience, but don't spend time debugging it inside a
//      container — that's expected, not a bug in your code.
//
// Why bootstrap-and-gossip instead of just broadcast? Because it's what
// real P2P systems actually do, for the same reason: BitTorrent, Chord,
// and Kademlia (week 12) all assume broadcast doesn't work past a single
// LAN segment, so they always give you at least one known peer to start
// from and grow the network by exchanging peer lists from there. You're
// building the same pattern, just at a much smaller scale.
//

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
/// Sprint 3: Peer discovery and mesh formation.
///
/// Required: TCP-based bootstrap + peer exchange (Initialize,
/// BuildPeerListMessage, ProcessPeerListMessage, RegisterPeer).
/// Optional, not graded: UDP LAN broadcast (StartLanBroadcast and below).
/// </summary>
public class PeerDiscovery
{
    private readonly ConcurrentDictionary<string, PeerInfo> _knownPeers = new();

    /// <summary>
    /// Fired when a peer you didn't already know about is learned — either
    /// because you connected to them directly (RegisterPeer) or because
    /// another peer told you about them (ProcessPeerListMessage). Whatever
    /// owns your TCP connections (likely Program.cs) should subscribe to
    /// this and call Client.ConnectAsync(peer.Address, peer.Port) so the
    /// mesh actually grows instead of just knowing names.
    /// </summary>
    public event Action<PeerInfo>? OnPeerDiscovered;

    /// <summary>Fired when a peer is removed (e.g. after a heartbeat timeout).</summary>
    public event Action<PeerInfo>? OnPeerLost;

    public int TcpPort { get; private set; }
    public string LocalPeerId { get; } = Guid.NewGuid().ToString()[..8];

    // ================================================================
    // REQUIRED: Bootstrap + Peer Exchange (TCP)
    // ================================================================

    /// <summary>
    /// Call this once, at startup, with the same port you pass to
    /// Server.Start(). There's nothing to connect to yet — that happens
    /// via /connect (bootstrap) or when a PeerList message teaches you
    /// about someone new.
    ///
    /// TODO: Store tcpPort in TcpPort.
    /// </summary>
    public void Initialize(int tcpPort)
    {
        throw new NotImplementedException("Implement Initialize() - see TODO in comments above");
    }

    /// <summary>
    /// Register a peer you've directly connected to (or that connected to
    /// you) — call this from wherever your TCP connection logic lives
    /// (Client.OnConnected / Server.OnClientConnected) once a connection
    /// succeeds, using that connection's real remote address and the port
    /// the peer tells you it's listening on.
    ///
    /// TODO:
    /// 1. If _knownPeers already has this peer's Id, just return (nothing
    ///    new to announce).
    /// 2. Otherwise add it with _knownPeers.TryAdd(peer.Id, peer) and
    ///    fire OnPeerDiscovered(peer).
    /// </summary>
    public void RegisterPeer(PeerInfo peer)
    {
        throw new NotImplementedException("Implement RegisterPeer() - see TODO in comments above");
    }

    /// <summary>
    /// Build a Message of type MessageType.PeerList containing everyone you
    /// currently know about, including yourself (so the receiver learns
    /// your id and listen port, not just the peers you already know).
    ///
    /// TODO:
    /// 1. Build a List&lt;PeerInfo&gt; containing _knownPeers.Values, plus one
    ///    more PeerInfo for yourself: { Id = LocalPeerId, Port = TcpPort }
    ///    (you can leave Address blank for yourself — the receiver already
    ///    knows your IP from the live connection you're sending this over)
    /// 2. Serialize that list to JSON with
    ///    System.Text.Json.JsonSerializer.Serialize(...)
    /// 3. Return a new Message with Type = MessageType.PeerList,
    ///    Sender = LocalPeerId, Content = that JSON string
    ///
    /// Send the result over EVERY new connection, right after it's
    /// established — both the side that dialed and the side that
    /// accepted should send one, so peer lists spread both directions.
    /// </summary>
    public Message BuildPeerListMessage()
    {
        throw new NotImplementedException("Implement BuildPeerListMessage() - see TODO in comments above");
    }

    /// <summary>
    /// Handle an incoming MessageType.PeerList message from a connected
    /// peer. This is the step that turns "I connected to one peer" into
    /// "I'm part of the mesh": if peer B tells you about peer C, and you
    /// don't already know C, you now do — and OnPeerDiscovered will tell
    /// whoever's listening to go connect to them.
    ///
    /// TODO:
    /// 1. Deserialize message.Content back into a List&lt;PeerInfo&gt; with
    ///    System.Text.Json.JsonSerializer.Deserialize&lt;List&lt;PeerInfo&gt;&gt;(...)
    /// 2. For the entry representing the sender (Id == message.Sender):
    ///    fill in its Address from the live connection you received this
    ///    on (you know that address directly — don't trust a self-reported
    ///    one), then treat it like any other newly-learned peer.
    /// 3. For every entry in the list:
    ///    a. Skip it if Id == LocalPeerId (that's you)
    ///    b. Skip it if _knownPeers already contains it
    ///    c. Otherwise: _knownPeers.TryAdd(...) and fire OnPeerDiscovered
    ///       so the caller can open a new connection to it
    /// </summary>
    public void ProcessPeerListMessage(Message message, IPAddress senderAddress)
    {
        throw new NotImplementedException("Implement ProcessPeerListMessage() - see TODO in comments above");
    }

    /// <summary>
    /// Get list of known peers (for the /peers command).
    /// </summary>
    public IEnumerable<PeerInfo> GetKnownPeers()
    {
        return _knownPeers.Values.ToList();
    }

    /// <summary>
    /// Remove a peer, e.g. after HeartbeatMonitor.OnConnectionFailed fires.
    ///
    /// TODO: _knownPeers.TryRemove(...); if it was actually present, fire
    /// OnPeerLost with the removed peer.
    /// </summary>
    public void RemovePeer(string peerId)
    {
        throw new NotImplementedException("Implement RemovePeer() - see TODO in comments above");
    }

    // ================================================================
    // OPTIONAL, NOT GRADED: UDP LAN broadcast auto-discovery
    //
    // Everything below is untested by the grader and entirely optional.
    // It's here in case you want a fun extra for local testing on a real
    // LAN — just don't count on it working in Docker, a VM, or across
    // subnets/VPNs. See the note at the top of this file for why.
    // ================================================================

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly int _broadcastPort = 5001;
    private Thread? _listenThread;
    private Thread? _broadcastThread;

    /// <summary>
    /// Optional: start broadcasting presence and listening for other peers
    /// on the local network. Not required, not graded.
    ///
    /// TODO (optional):
    /// 1. Create a new CancellationTokenSource
    /// 2. Create a UdpClient on _broadcastPort with broadcast enabled
    /// 3. Start ListenLoop and BroadcastLoop on background threads
    /// </summary>
    public void StartLanBroadcast()
    {
        throw new NotImplementedException("Optional, not graded - implement only if you want LAN auto-discovery for your own testing.");
    }

    /// <summary>
    /// Optional: periodically broadcast "PEER:{LocalPeerId}:{TcpPort}" to
    /// 255.255.255.255 on _broadcastPort. Not required, not graded.
    /// </summary>
    private void BroadcastLoop()
    {
        throw new NotImplementedException("Optional, not graded - see StartLanBroadcast().");
    }

    /// <summary>
    /// Optional: listen for "PEER:id:port" broadcasts from other peers on
    /// the LAN and call RegisterPeer for anything new. Not required, not
    /// graded.
    /// </summary>
    private void ListenLoop()
    {
        throw new NotImplementedException("Optional, not graded - see StartLanBroadcast().");
    }

    /// <summary>
    /// Optional: stop LAN broadcast discovery.
    /// </summary>
    public void StopLanBroadcast()
    {
        throw new NotImplementedException("Optional, not graded - cancel the token, close the UDP client, join the threads.");
    }
}
