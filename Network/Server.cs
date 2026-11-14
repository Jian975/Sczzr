// Jackson Welde (sprint 1), Xuejian Sundvall (sprint 2)
// CSCI 251 - Secure Distributed Messenger
//
// Server.cs - TCP server that listens for connections and receives messages.
// The server is a headless relay: it accepts client connections and broadcasts
// messages between them. It doesn't participate in the chat itself.
//
// See HINTS.md for TCP and threading reference.

using System.Collections.Concurrent;
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

	// <username, public key bytes>
	private readonly ConcurrentDictionary<string, byte[]> _publicKeys = new();

	//TODO: modify this if needed (wasn't provided)
	//roomname, client usernames
	private readonly ConcurrentDictionary<string, HashSet<string>> _rooms = new();

	// <username, endpoint>
	private readonly ConcurrentDictionary<string, string> _usernamesToEndpoints = new();

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
		Port = port;

		_cancellationTokenSource = new CancellationTokenSource();

		_listener = new TcpListener(IPAddress.Any, port);
		_listener.Start();

		IsListening = true;

		_ = AcceptClientsAsync();

		Console.WriteLine($"Server listening on port {port}");
	}

	/// <summary>
	///     Runs in the background, accepting clients as they connect.
	/// </summary>
	private async Task AcceptClientsAsync()
	{
		var token = _cancellationTokenSource!.Token;
		while (!token.IsCancellationRequested) // loops until canceled to connect any new clients
		{
			try
			{
				var client = await _listener!.AcceptTcpClientAsync(token);
				var endpoint = client.Client.RemoteEndPoint?.ToString() ?? ""; // gets the endpoint, or "" if null

				// vvv saving the new client in our clients dict
				lock (_clientsLock)
				{
					_clients[endpoint] = client;
				}

				OnClientConnected?.Invoke(endpoint); // fire the event
				_ = ReceiveFromClientAsync(client, endpoint); // start listening to the client
			}
			catch (ObjectDisposedException)
			{
				break;
			}
			catch (Exception exception)
			{
				Console.WriteLine($"Error accepting client: {exception.Message}");
			}
		}
	}

	/// <summary>
	///     Reads messages from one client until they disconnect.
	///     Uses length-prefix framing: first 4 bytes = payload length, then the JSON.
	/// </summary>
	private async Task ReceiveFromClientAsync(TcpClient client, string endpoint)
	{
		var token = _cancellationTokenSource!.Token;

		try
		{
			var stream = client.GetStream();

			while (true)
			{
				var lengthBytes = await MessageUtils.ReadBytesAsync(stream, 4, token); // reading length bytes
				var length = MessageUtils.LengthBytesToLength(lengthBytes); // converting length bytes to an integer

				// vvv checking that the bytes are within our size assumptions
				if (!MessageUtils.IsValidMessageLength(length))
				{
					Console.WriteLine($"Invalid message length from {endpoint}: {length}");
					return;
				}

				var payload = await MessageUtils.ReadBytesAsync(stream, length, token); // reading the payload bytes
				var message = MessageUtils.PayloadToMessage(payload);

				OnMessageReceived?.Invoke(endpoint, message); // fire the event
			}
		}
		catch (OperationCanceledException) { }
		catch (IOException) { }
		catch (Exception exception)
		{
			Console.WriteLine($"Error receiving from {endpoint}: {exception.Message}");
		}
		finally
		{
			DisconnectClient(client, endpoint);
		}
	}

	/// <summary>
	///     Removes a client from the list and cleans up.
	/// </summary>
	private void DisconnectClient(TcpClient client, string endpoint)
	{
		// vvv remove client from the clients list
		lock (_clientsLock)
		{
			_clients.Remove(endpoint);
		}

		client.Close();

		OnClientDisconnected?.Invoke(endpoint); // fire event
	}

	/// <summary>
	///     Sends a message to every connected client.
	/// </summary>
	public void Broadcast(Message message)
	{
		var (lengthBytes, payload) = MessageUtils.MessageToBytes(message);

		// vvv copying the clients so that we don't need to hold the lock while sending messages
		TcpClient[] clients;
		lock (_clientsLock)
		{
			clients = _clients.Values.ToArray();
		}

		// vvv sending the message to each client one-by-one
		foreach (var client in clients)
		{
			AttemptToSendMessage(client, lengthBytes, payload);
		}
	}

	/// <summary>
	///     Sends a message to exactly one connected client, identified by the same
	///     endpoint string OnClientConnected/OnMessageReceived gave you. This is what
	///     makes room-scoped delivery and per-recipient encryption possible - Broadcast
	///     alone can't send different content to different clients, and chat rooms
	///     (Sprint 2+) need exactly that: a message encrypted separately per recipient,
	///     delivered only to that recipient.
	/// </summary>
	public void SendTo(string endpoint, Message message)
	{
		// vvv getting the client at this endpoint, or ignoring it if it already disconnected
		TcpClient client;
		lock (_clientsLock)
		{
			if (!_clients.TryGetValue(endpoint, out client))
			{
				return;
			}
		}

		var (lengthBytes, payload) = MessageUtils.MessageToBytes(message);
		AttemptToSendMessage(client, lengthBytes, payload);
	}

	/// <summary>
	///     Sends the inputted message bytes to the client, or prints an error on failure.
	/// </summary>
	private void AttemptToSendMessage(TcpClient client, byte[] lengthBytes, byte[] payload)
	{
		try
		{
			var stream = client.GetStream();

			// vvv writing all bytes to the client
			stream.Write(lengthBytes, 0, lengthBytes.Length);
			stream.Write(payload, 0, payload.Length);
		}
		catch (Exception exception)
		{
			Console.WriteLine($"Error sending message: {exception.Message}");
		}
	}

	/// <summary>
	///     Shut everything down.
	/// </summary>
	public void Stop()
	{
		_cancellationTokenSource.Cancel();

		_listener.Stop();

		IsListening = false;

		lock (_clientsLock)
		{
			foreach (var client in _clients.Values)
			{
				client.Close();
			}

			_clients.Clear();
		}
	}

	/// <summary>
	///     Creates a new message room with a specific name.
	/// </summary>
	public void CreateRoom(string roomName)
	{
		if (!_rooms.ContainsKey(roomName))
		{
			_rooms.TryAdd(roomName, new HashSet<string>()); // adding the room to the room dict
		}
	}

	/// <summary>
	///     Adds a new user to a room.
	/// </summary>
	public void AddToRoom(string username, string roomName)
	{
		CreateRoom(roomName); // creates a room if it didn't exist yet
		_rooms[roomName].Add(username); // adds the new user
	}

	/// <summary>
	///     Removes a user from a room.
	/// </summary>
	public void RemoveFromRoom(string username, string roomName)
	{
		_rooms[roomName].Remove(username); // removes the user
	}

	/// <summary>
	///     Returns a list of all stored chat rooms.
	/// </summary>
	public List<string> ListRooms()
	{
		return _rooms.Keys.ToList();
	}

	/// <summary>
	///     Gets a list of all active users.
	/// </summary>
	public string GetAllUsers()
	{
		return string.Join(", ", _usernamesToEndpoints.Keys);
	}

	/// <summary>
	///     Gets a list of all members in an inputted chat room.
	/// </summary>
	public string GetRoomMembers(string roomName)
	{
		var members = _rooms[roomName].ToList();
		return string.Join(", ", members);
	}

	/// <summary>
	///     Returns true if the entered user is a member of the chat room, and false otherwise.
	/// </summary>
	public bool IsRoomMember(string username, string roomName)
	{
		return _rooms.TryGetValue(roomName, out var members) && members.Contains(username);
	}

	/// <summary>
	///     Saves a user's public key to the public key dictionary.
	/// </summary>
	public void AddPublicKey(string username, byte[] publicKey)
	{
		_publicKeys.TryAdd(username, publicKey);
	}

	/// <summary>
	///     Sends all public keys to all connected clients. This is used for peer discovery and key exchange.
	///     Message format: username:base64(publicKey),username:base64(publicKey),...
	/// </summary>
	public void BroadcastPublicKeys()
	{
		// vvv creates a string of all stored public keys
		var publicKeyString = "";
		foreach (var kvp in _publicKeys)
		{
			publicKeyString += kvp.Key + ":" + Convert.ToBase64String(kvp.Value) + ",";
		}

		var message = new Message
		{
			Sender = "Server",
			Content = publicKeyString.TrimEnd(','), // Remove the trailing comma
			Type = MessageType.KeyExchange,
			Timestamp = DateTime.Now
		};
		Broadcast(message); // sends the public key string to all client
	}

	/// <summary>
	///     Saves a specific username to a specific endpoint.
	/// </summary>
	public void AddUsername(string username, string endpoint)
	{
		_usernamesToEndpoints.TryAdd(username, endpoint);
	}

	/// <summary>
	///     Removes the saved username for a specific endpoint.
	/// </summary>
	public void RemoveUsername(string endpoint)
	{
		var username = _usernamesToEndpoints.FirstOrDefault(kvp => kvp.Value == endpoint).Key;
		if (username != null)
		{
			_usernamesToEndpoints.TryRemove(username, out _);
		}
	}

	/// <summary>
	///     Gets the endpoint for a user by their username.
	/// </summary>
	public string? GetEndpointByUsername(string username)
	{
		if (_usernamesToEndpoints.TryGetValue(username, out var endpoint))
		{
			return endpoint;
		}

		return null;
	}
}