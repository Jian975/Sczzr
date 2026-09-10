// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger
//
// PROVIDED - No implementation required
// This data model is complete. You may add properties if needed.
//

namespace SecureMessenger.Core;

/// <summary>
/// Message types for the wire protocol.
///
/// Sprint 1: Only Text is used
/// Sprint 2: Add KeyExchange and SessionKey for encryption setup
/// Sprint 3: Add Heartbeat and PeerList for P2P features
/// </summary>
public enum MessageType
{
    Text,           // Regular chat message
    KeyExchange,    // Sprint 2: Public key exchange
    SessionKey,     // Sprint 2: Encrypted session key
    Heartbeat,      // Sprint 3: Connection health check
    PeerList        // Sprint 3: Required peer-exchange gossip (PeerDiscovery.BuildPeerListMessage)
}

/// <summary>
/// Represents a message in the system
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Message type - determines how the message is processed.
    /// Sprint 1: Always MessageType.Text
    /// Sprint 2-3: Use other types for protocol messages
    /// </summary>
    public MessageType Type { get; set; } = MessageType.Text;

    // Sprint 2: Security fields
    public byte[]? Signature { get; set; }
    public byte[]? EncryptedContent { get; set; }
    public byte[]? PublicKey { get; set; }

    // Sprint 2-3: Target peer for directed/routed messages (/msg @peer)
    public string? TargetPeerId { get; set; }

    // Sprint 2+: Which chat room this message belongs to, if any (/msg #room).
    // Null/empty means it's not a room message (e.g. a direct message, or a
    // protocol message like KeyExchange/Heartbeat/PeerList).
    public string? RoomName { get; set; }

    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}";
    }
}
