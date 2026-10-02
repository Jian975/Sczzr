// Jackson Welde
// CSCI 251 - Secure Distributed Messenger
//
// Server.cs - TCP server that listens for connections and receives messages.
// The server is a headless relay: it accepts client connections and broadcasts
// messages between them. It doesn't participate in the chat itself.
//
// See HINTS.md for TCP and threading reference.

using System.Net;
using System.Net.Sockets;
using SecureMessenger.Core;

namespace SecureMessenger.Network;

/// <summary>
///     TCP server - listens for incoming connections and relays messages.
/// </summary>
public class Server
{
	private readonly Dictionary<string, TcpClient> _clients = new();
	private readonly object _clientsLock = new();
	private CancellationTokenSource? _cancellationTokenSource;
	private TcpListener? _listener;

	public int Port { get; private set; }
	public bool IsListening { get; private set; }

	/// <summary>
	///     How many clients are currently connected.
	/// </summary>
	public int ClientCount
	{
		get
		{
			lock (_clientsLock)
			{
				return _clients.Count;
			}
		}
	}

	// Program.cs hooks into these to react to events.
	// Usage: server.OnClientConnected += (endpoint) => { ... };
	public event Action<string>? OnClientConnected; // e.g. "192.168.1.5:54321"
	public event Action<string>? OnClientDisconnected;
	public event Action<string, Message>? OnMessageReceived; // (endpoint that sent it, the message)

	/// <summary>
	///     Start listening on the given port.
	/// </summary>
	public void Start(int port)
	{
		Port =  port;
		
		_cancellationTokenSource = new CancellationTokenSource();
		
		_listener = new TcpListener(IPAddress.Any, port);
		_listener.Start();
		
		IsListening = true;
		
		_ = AcceptClientsAsync();
		
		Console.WriteLine($"Server listening on port {port}");
	}

	/// <summary>
	///     Runs in the background, accepting clients as they connect.
	///     TODO:
	///     Loop until cancellation is requested. Each iteration:
	///     - await _listener.AcceptTcpClientAsync(token)
	///     - Grab the endpoint string from client.Client.RemoteEndPoint
	///     - Add the client to _clients, keyed by that endpoint string (lock first!)
	///     - Fire OnClientConnected
	///     - Spin up ReceiveFromClientAsync for this client on another Task
	///     Catch OperationCanceledException (that's normal shutdown, just break).
	///     Catch anything else and log it.
	/// </summary>
	private async Task AcceptClientsAsync()
	{
		throw new NotImplementedException("Implement AcceptClientsAsync()");
	}

	/// <summary>
	///     Reads messages from one client until they disconnect.
	///     Uses length-prefix framing: first 4 bytes = payload length, then the JSON.
	///     TODO:
	///     Get the NetworkStream, allocate a 4-byte length buffer, then loop:
	///     - Read 4 bytes for the length. If bytesRead is 0, they disconnected.
	///     - Convert to int with BitConverter.ToInt32, sanity-check it (> 0, &lt; 1MB)
	///     - Allocate a buffer and read the full payload. Remember that ReadAsync
	///     might not give you everything in one call - loop until you have it all.
	///     - Decode with Encoding.UTF8.GetString, deserialize with JsonSerializer
	///     - Fire OnMessageReceived with (endpoint, result) - the caller needs to know
	///     WHICH client this came from to route replies, check room membership, etc.
	///     Wrap the whole thing in try/catch - OperationCanceledException is normal.
	///     Always call DisconnectClient in a finally block.
	/// </summary>
	private async Task ReceiveFromClientAsync(TcpClient client, string endpoint)
	{
		throw new NotImplementedException("Implement ReceiveFromClientAsync()");
	}

	/// <summary>
	///     Removes a client from the list and cleans up.
	///     TODO: Lock, remove from _clients, close the client, fire OnClientDisconnected.
	/// </summary>
	private void DisconnectClient(TcpClient client, string endpoint)
	{
		throw new NotImplementedException("Implement DisconnectClient()");
	}

	/// <summary>
	///     Sends a message to every connected client.
	///     TODO:
	///     Serialize the message to JSON, convert to bytes, build the 4-byte length prefix.
	///     Then grab a copy of _clients (lock!), and for each connected client, write
	///     the length prefix + payload to their NetworkStream. If writing to one client
	///     fails, catch the exception and keep going - don't kill the whole broadcast.
	/// </summary>
	public void Broadcast(Message message)
	{
		throw new NotImplementedException("Implement Broadcast()");
	}

	/// <summary>
	///     Sends a message to exactly one connected client, identified by the same
	///     endpoint string OnClientConnected/OnMessageReceived gave you. This is what
	///     makes room-scoped delivery and per-recipient encryption possible - Broadcast
	///     alone can't send different content to different clients, and chat rooms
	///     (Sprint 2+) need exactly that: a message encrypted separately per recipient,
	///     delivered only to that recipient.
	///     TODO:
	///     Look up the endpoint in _clients (lock first!). If found, serialize the
	///     message to JSON, build the length prefix, and write both to that client's
	///     NetworkStream. If the endpoint isn't found (already disconnected), just
	///     return - don't throw.
	/// </summary>
	public void SendTo(string endpoint, Message message)
	{
		throw new NotImplementedException("Implement SendTo()");
	}

	/// <summary>
	///     Shut everything down.
	///     TODO: Cancel the token, stop the listener, set IsListening = false,
	///     close all clients (with locking), clear the list.
	/// </summary>
	public void Stop()
	{
		throw new NotImplementedException("Implement Stop()");
	}
}