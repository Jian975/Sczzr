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
		CancellationToken token = _cancellationTokenSource!.Token;
		while (!token.IsCancellationRequested) // loops until canceled to connect any new clients
		{
			try
			{
				TcpClient client = await _listener!.AcceptTcpClientAsync(token);
				string endpoint = client.Client.RemoteEndPoint?.ToString() ?? ""; // gets the endpoint, or "" if null

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
		CancellationToken token = _cancellationTokenSource!.Token;

		try
		{
			NetworkStream stream = client.GetStream();

			while (true)
			{
				byte[] lengthBytes = await MessageUtils.ReadBytesAsync(stream, 4, token); // reading length bytes
				int length = MessageUtils.lengthBytesToLength(lengthBytes); // converting length bytes to an integer

				// vvv checking that the bytes are within our size assumptions
				if (!MessageUtils.isValidMessageLength(length))
				{
					Console.WriteLine($"Invalid message length from {endpoint}: {length}");
					return;
				}

				byte[] payload = await MessageUtils.ReadBytesAsync(stream, length, token); // reading the payload bytes
				Message message = MessageUtils.payloadToMessage(payload);

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
		(byte[] lengthBytes, byte[] payload) = MessageUtils.messageToBytes(message);

		// vvv copying the clients so that we don't need to hold the lock while sending messages
		TcpClient[] clients;
		lock (_clientsLock)
		{
			clients = _clients.Values.ToArray();
		}

		// vvv sending the message to each client one-by-one
		foreach (var client in clients)
		{
			attemptToSendMessage(client, lengthBytes, payload);
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

		(byte[] lengthBytes, byte[] payload) = MessageUtils.messageToBytes(message);
		attemptToSendMessage(client, lengthBytes, payload);
		
	}
	
	/// <summary>
	///     Sends the inputted message bytes to the client, or prints an error on failure.
	/// </summary>
	private void attemptToSendMessage(TcpClient client, byte[] lengthBytes, byte[] payload)
	{
		try
		{
			NetworkStream stream = client.GetStream();

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
	///     TODO: Cancel the token, stop the listener, set IsListening = false,
	///     close all clients (with locking), clear the list.
	/// </summary>
	public void Stop()
	{
		throw new NotImplementedException("Implement Stop()");
	}
}