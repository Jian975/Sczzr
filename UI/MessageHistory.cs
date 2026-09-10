// [Your Name Here]
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 3: P2P & Advanced Features
// Due: Week 14
//

using System.Text.Json;
using SecureMessenger.Core;
using SecureMessenger.Security;

namespace SecureMessenger.UI;

/// <summary>
/// Sprint 3: Message history storage and retrieval.
/// Persists messages to a JSON file for retrieval across sessions.
///
/// ENCRYPTION IS REQUIRED, NOT OPTIONAL. History is written to a plain
/// file on disk - anyone with file access can read it unless you encrypt
/// it. You've already built AesEncryption for Sprint 2; reuse it here.
///
/// Features:
/// - Thread-safe message storage
/// - JSON serialization, then AES-encrypted before hitting disk
/// - Automatic loading (decrypt then deserialize) on startup
/// - Configurable history display limit
///
/// File Format: AES-encrypted bytes; decrypted content is a JSON array of Message objects
/// Default file: "message_history.dat"
/// </summary>
public class MessageHistory
{
    private readonly string _historyFile;
    private readonly AesEncryption _encryption;
    private readonly List<Message> _messages = new();
    private readonly object _lock = new();

    /// <summary>
    /// Create a MessageHistory with the AES key used to encrypt/decrypt the
    /// history file, and an optional custom file path. Automatically loads
    /// and decrypts existing history from file.
    ///
    /// TODO: Implement the following:
    /// 1. Store the history file path
    /// 2. Create an AesEncryption instance from the given key and store it
    ///    (you can reuse a session's AES key, or generate/persist a
    ///    dedicated history key - either is fine, just document which)
    /// 3. Call Load() to load and decrypt existing history
    /// </summary>
    public MessageHistory(byte[] encryptionKey, string historyFile = "message_history.dat")
    {
        throw new NotImplementedException("Implement constructor - see TODO in comments above");
    }

    /// <summary>
    /// Save a message to history and persist to file.
    ///
    /// TODO: Implement the following:
    /// 1. Lock on _lock for thread safety
    /// 2. Add the message to _messages list
    /// 3. Call PersistToFile() to save to disk
    /// </summary>
    public void SaveMessage(Message message)
    {
        throw new NotImplementedException("Implement SaveMessage() - see TODO in comments above");
    }

    /// <summary>
    /// Load history from file on startup.
    ///
    /// TODO: Implement the following:
    /// 1. Check if the history file exists
    /// 2. If it exists:
    ///    a. Read the file contents as bytes (File.ReadAllBytes - it's
    ///       encrypted, not text, so don't read it as a string)
    ///    b. Decrypt with _encryption.Decrypt(...) to get back the JSON string
    ///    c. Deserialize from JSON to List<Message>
    ///    d. Lock on _lock and replace _messages with loaded data
    /// 3. Handle exceptions (file errors, JSON errors, decryption errors):
    ///    a. Print error message but don't crash
    ///    b. Start with empty history if load fails
    /// </summary>
    public void Load()
    {
        throw new NotImplementedException("Implement Load() - see TODO in comments above");
    }

    /// <summary>
    /// Write the current messages to the history file, encrypted.
    ///
    /// TODO: Implement the following:
    /// 1. Serialize _messages to JSON
    ///    - Use JsonSerializerOptions with WriteIndented = true for readability
    /// 2. Encrypt the JSON string with _encryption.Encrypt(...) to get bytes
    /// 3. Write those bytes to the history file (File.WriteAllBytes - the
    ///    file is encrypted binary now, not human-readable JSON)
    /// 4. Handle exceptions:
    ///    a. Print error message but don't crash
    ///
    /// Note: This is called while holding _lock, so don't lock again
    /// </summary>
    private void PersistToFile()
    {
        throw new NotImplementedException("Implement PersistToFile() - see TODO in comments above");
    }

    /// <summary>
    /// Get messages from history.
    ///
    /// TODO: Implement the following:
    /// 1. Lock on _lock for thread safety
    /// 2. Order messages by Timestamp descending (newest first)
    /// 3. If limit is specified, take only that many messages
    /// 4. Return as a new List (don't return the internal list)
    ///
    /// Hint: Use LINQ OrderByDescending, Take, and ToList
    /// </summary>
    public IEnumerable<Message> GetHistory(int? limit = null)
    {
        throw new NotImplementedException("Implement GetHistory() - see TODO in comments above");
    }

    /// <summary>
    /// Display history to console.
    ///
    /// TODO: Implement the following:
    /// 1. Print a header: "--- Message History (last N messages) ---"
    /// 2. Get history with the specified limit
    /// 3. Reverse the order (so oldest is first, newest is last)
    /// 4. Print each message using its ToString()
    /// 5. Print a footer: "--- End of History ---"
    /// </summary>
    public void ShowHistory(int limit = 50)
    {
        throw new NotImplementedException("Implement ShowHistory() - see TODO in comments above");
    }

    /// <summary>
    /// Clear all history from memory and disk.
    ///
    /// TODO: Implement the following:
    /// 1. Lock on _lock for thread safety
    /// 2. Clear the _messages list
    /// 3. Delete the history file if it exists
    /// </summary>
    public void Clear()
    {
        throw new NotImplementedException("Implement Clear() - see TODO in comments above");
    }
}
