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
using System.Text;
using System.Text.Json;
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

	private const int MEGABYTE_LENGTH = 1048576;

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
				byte[] lengthBytes = await ReadBytesAsync(stream, 4, token); // getting the bytes with the length
				int length = BitConverter.ToInt32(lengthBytes, 0); // converting to an integer

				// vvv checking that the bytes are within our size assumptions
				if (length <= 0 || length >= MEGABYTE_LENGTH)
				{
					Console.WriteLine($"Invalid message length from {endpoint}: {length}");
					return;
				}

				byte[] payload = await ReadBytesAsync(stream, length, token); // reading the payload bytes
				// vvv converting the bytes to a message
				string json = Encoding.UTF8.GetString(payload);
				Message? message = JsonSerializer.Deserialize<Message>(json);

				// vvv checking that the message exists
				if (message == null)
				{
					Console.WriteLine($"Invalid message from {endpoint}.");
					continue;
				}

				OnMessageReceived?.Invoke(endpoint, message); // fire the event
			}
		}
		catch (OperationCanceledException) { }
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
	///     Reads the specified length bytes from the stream until all are received.
	/// </summary>
	private async Task<byte[]> ReadBytesAsync(NetworkStream stream, int length, CancellationToken token)
	{
		byte[] buffer = new byte[length];
		int bytesRead = 0;

		// vvv looping since we might not get everything in one call
		while (bytesRead < length)
		{
			int read = await stream.ReadAsync(
				buffer.AsMemory(bytesRead, length - bytesRead),
				token);

			if (read == 0)
			{
				throw new IOException("Client disconnected.");
			}

			bytesRead += read;
		}

		return buffer;
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
		string json = JsonSerializer.Serialize(message);
		byte[] payload = Encoding.UTF8.GetBytes(json);

		byte[] lengthBytes = BitConverter.GetBytes(payload.Length);

		// vvv copying the clients (we copy so that we don't need to lock for all the networking later)
		TcpClient[] clients;
		lock (_clientsLock)
		{
			clients = _clients.Values.ToArray();
		}

		// vvv sending the message to each client one-by-one
		foreach (var client in clients)
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
				Console.WriteLine($"Error broadcasting message: {exception.Message}");
			}
		}
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