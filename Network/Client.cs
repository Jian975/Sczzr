// [Athena Ching (sprint 1, sprint 2)]
// CSCI 251 - Secure Distributed Messenger
//
// Client.cs - Connects to a server and handles sending/receiving messages.
// The server (Server.cs) waits for connections; this class initiates them.
//
// Test setup: Terminal 1 runs /listen, Terminal 2 runs /connect
// See HINTS.md for TCP and length-prefix framing reference.

using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecureMessenger.Core;
using SecureMessenger.Security;

namespace SecureMessenger.Network;

/// <summary>
///     TCP client - connects to a server, sends messages, receives messages.
/// </summary>
public class Client
{
	private AesEncryption? _aesEncryption;
	private CancellationTokenSource? _cancellationTokenSource;
	private TcpClient? _client;

	private KeyExchange? _keyExchange;
	private MessageSigner? _messageSigner;

	private readonly ConcurrentDictionary<string, byte[]>? _peerPublicKeys = new();

	private byte[]? _privateKey;

	private byte[]? _publicKey;
	private bool _publicKeysReceived = false;

	private readonly ConcurrentDictionary<string, string[]>? _roomMembers = new();

	private RSA? _rsa;
	private string _serverEndpoint = "";

	private readonly ConcurrentDictionary<string, byte[]>? _sessionKeys = new(); //peer private aes keys
	private NetworkStream? _stream;

	private bool _userListReceived;

	private object lockObject = new();
	public bool IsConnected => _client?.Connected ?? false;

	public string _username { get; set; } = "User";
	public event Action<string>? OnConnected;
	public event Action<string>? OnDisconnected;
	public event Action<Message>? OnMessageReceived;

	/// <summary>
	///     If the connection is successful, it initializes the network stream,
	///     saves the server endpoint, fires the OnConnected event,
	///     and starts receiving messages in a background task.
	///     If any exceptions occur during the connection process,
	///     they are caught and logged, and the method returns false.
	///     @param host The server's hostname or IP address.
	///     @param port The server's port number.
	///     @return True if the connection is successful, false otherwise.
	///     td:
	///     Create a CancellationTokenSource and a new TcpClient, then
	///     await ConnectAsync(host, port). Grab the NetworkStream, save the
	///     endpoint string, fire OnConnected, and start ReceiveAsync on a
	///     background Task. Return true.
	///     If anything throws, log the error and return false.
	/// </summary>
	public async Task<bool> ConnectAsync(string host, int port)
	{
		try
		{
			// Create a CancellationTokenSource and a new TcpClient
			_cancellationTokenSource = new CancellationTokenSource(); //TimeSpan.FromSeconds(5)
			_client = new TcpClient();

			// Connect to the server
			await _client.ConnectAsync(host, port, _cancellationTokenSource.Token);

			// Grab the NetworkStream
			_stream = _client.GetStream();

			// Save the endpoint string
			_serverEndpoint = _client.Client.RemoteEndPoint?.ToString() ?? $"{host}:{port}";

			_keyExchange = new KeyExchange();
			_publicKey = _keyExchange.GetPublicKey();
			_privateKey = _keyExchange.GetPrivateKey();
			//get private key bytes
			_rsa = RSA.Create(2048);
			_rsa.ImportRSAPublicKey(_publicKey, out _);
			_rsa.ImportRSAPrivateKey(_privateKey, out _);
			_messageSigner = new MessageSigner(_rsa); //create message signer with the RSA instance
			
			OnConnected?.Invoke(_serverEndpoint);

			//send public key to server
			var keyExchangeMessage = new Message
			{
				Sender = _username,
				PublicKey = _publicKey,
				Type = MessageType.KeyExchange,
				Timestamp = DateTime.Now
			};

			Send(keyExchangeMessage);

			// Start ReceiveAsync on a background Task
			_ = Task.Run(() => ReceiveAsync());

			return true;
		}
		catch (SocketException)
		{
			Console.WriteLine("Could not reach server");
			return false;
		}
		catch (IOException)
		{
			Console.WriteLine("Connection to server lost");
			return false;
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
			return false;
		}
	}

	/// <summary>
	///     Background receive loop. Same length-prefix framing as Server:
	///     read 4 bytes for the length, then read that many bytes of JSON.
	///     td:
	///     Allocate a 4-byte buffer. Loop while connected and not cancelled:
	///     - Read the 4-byte length prefix. 0 bytes = server disconnected.
	///     - Convert to int, validate it (> 0, &lt; 1MB)
	///     - Read the full payload (loop - ReadAsync might not return it all at once)
	///     - Deserialize the JSON into a Message and fire OnMessageReceived
	///     Catch OperationCanceledException (normal shutdown).
	///     In the finally block, fire OnDisconnected.
	///     TODO: handle decrypt
	/// </summary>
	private async Task ReceiveAsync()
	{
		var token = _cancellationTokenSource!.Token;
		var buffer = new byte[4];

		var responseBytes = new List<byte>();
		try
		{
			while (IsConnected && !token.IsCancellationRequested)
			{
				// Read the 4-byte length prefix
				var prefix = await MessageUtils.ReadBytesAsync(_stream!, 4, token);

				// Convert the length bytes to an integer
				var length = MessageUtils.LengthBytesToLength(prefix);

				// Validate the length (> 0 and < 1MB)
				if (!MessageUtils.IsValidMessageLength(length))
				{
					break;
				}

				var payload = await MessageUtils.ReadBytesAsync(_stream!, length, token);

				// Deserialize the JSON into a Message
				var message = MessageUtils.PayloadToMessage(payload);

				if (message.Type == MessageType.KeyExchange)
				{
					if (message.Sender != "Server")
					{
						_peerPublicKeys!.TryAdd(message.Sender, message.PublicKey!);
					}
					else
					{
						var content = message.Content;
						var pairs = content.Split(',');
						foreach (var pair in pairs)
						{
							var keyValue = pair.Split(':');
							if (keyValue.Length == 2)
							{
								var username = keyValue[0].Trim();
								var publicKey = keyValue[1].Trim();
								// Process each key-value pair as needed
								_ = _peerPublicKeys!.TryAdd(username, Convert.FromBase64String(publicKey));
							}
						}
					}
				}
				else if (message.Type == MessageType.SessionKey)
				{
					var username = message.Sender;
					var encryptedSessionKey = message.EncryptedContent!;

					ReceiveEncryptedSessionKey(username, encryptedSessionKey);
					/**
					_ = _sessionKeys!.TryAdd(username, _keyExchange!.DecryptSessionKey(encryptedSessionKey));
					**/
				}
				else if (message.Sender == "Server" && message.Request == "list room members")
				{
					var roomName = message.RoomName!;
					var members = message.Content.Split(',',
						StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
					_roomMembers![roomName] = members;
					_userListReceived = true;
				}
				else if (message.Sender == "Server" && message.Request == "list all users")
				{
					var members = message.Content.Split(',',
						StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
					_roomMembers!["all"] = members;
					_userListReceived = true;
				}
				else if (message.Type == MessageType.Text && message.EncryptedContent != null)
				{
					// Handle text message
					var signatureValid = _messageSigner!.VerifyData(message.EncryptedContent, message.Signature!,
						_peerPublicKeys![message.Sender]);
					if (!signatureValid)
					{
						Console.WriteLine("Invalid signature for text message from " + message.Sender);
						return;
					}
					else
					{
						if (!_sessionKeys!.TryGetValue(message.Sender, out var sessionKey))
						{
							Console.WriteLine("Session key not found for " + message.Sender);
							continue;
						}

						_aesEncryption = new AesEncryption(sessionKey);
						var decryptedContent = _aesEncryption.Decrypt(message.EncryptedContent!);

						var decryptedMessage = JsonSerializer.Deserialize<Message>(decryptedContent)!;

						OnMessageReceived?.Invoke(decryptedMessage);
					}
				}
				else
				{
					// Fire the OnMessageReceived event
					OnMessageReceived?.Invoke(message);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// Normal shutdown
		}
		catch (IOException)
		{
			Console.WriteLine("Connection to server lost");
		}
		catch (Exception ex)
		{
			Console.WriteLine(ex.Message);
		}
		finally
		{
			//fire the OnDisconnected event
			OnDisconnected?.Invoke(_serverEndpoint);
		}
	}

	/// <summary>
	///     Sends an AES key to a user with RSA key exchange.
	/// </summary>
	public void SendEncryptedSessionKey(string username)
	{
		byte[]? peerPublicKey;
		if (!_peerPublicKeys!.TryGetValue(username, out peerPublicKey) || peerPublicKey == null)
		{
			Console.WriteLine("{Client.cs SendEncryptedSessionKey} Peer public key not found for " + username);
			return;
		}

		// create encrypted session key from peer public key
		_keyExchange!.ReceivePublicKey(peerPublicKey);
		var encryptedSessionKey = _keyExchange!.CreateEncryptedSessionKey(); // placeholder

		// store this peer's symmetric session key (non-encrypted)
		_sessionKeys!.TryAdd(username, _keyExchange!.SessionKey!);
		var sessionKeyMessage = new Message
		{
			Type = MessageType.SessionKey,
			Sender = _username,
			TargetPeerId = username,
			EncryptedContent = encryptedSessionKey
		};

		Send(sessionKeyMessage);
	}

	/// <summary>
	///     Handles the receiver side of the key exchange process. Stores AES keys for each peer.
	/// </summary>
	public void ReceiveEncryptedSessionKey(string username, byte[] encryptedSessionKey)
	{
		// vvv get the public key of the sender
		byte[]? peerPublicKey;
		if (!_peerPublicKeys!.TryGetValue(username, out peerPublicKey) || peerPublicKey == null)
		{
			Console.WriteLine("{Client.cs ReceiveEncryptedSessionKey} Peer public key not found for " + username);
			return;
		}

		_keyExchange!.ReceivePublicKey(peerPublicKey); // convert the key from bytes
		_keyExchange!.ReceiveEncryptedSessionKey(encryptedSessionKey); // get the encrypted AES key
		_sessionKeys!.TryAdd(username, _keyExchange!.SessionKey!); // store this AES key
	}

	/// <summary>
	///     Send a message to the server.
	///     td:
	///     Check if we're actually connected first. Then serialize the message
	///     to JSON, convert to bytes, build the 4-byte length prefix, and write
	///     both to the stream. Catch and log any exceptions.
	/// </summary>
	public void Send(Message message)
	{
		if (IsConnected)
		{
			try
			{
				// Convert message to bytes, and build the 4-byte length prefix
				var (prefix, payload) = MessageUtils.MessageToBytes(message);

				// Write the length prefix and payload to the stream
				_stream!.Write(prefix.Concat(payload).ToArray(), 0, prefix.Length + payload.Length);
			}
			catch (IOException)
			{
				Console.WriteLine("Connection to server lost");
			}
			catch (NullReferenceException)
			{
				Console.WriteLine("Null reference occurred while sending message");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}

	/// <summary>
	///     Sends a message directly to an inputted user.
	/// </summary>
	public void SendTo(string username, Message message)
	{
		if (IsConnected)
		{
			try
			{
				if (!_sessionKeys!.ContainsKey(username)) // checks if we already have an AES key with this user
				{
					SendEncryptedSessionKey(username); // if no AES key, do key exchange
				}

				// vvv retrieve AES session key
				byte[]? sessionKey;
				if (!_sessionKeys!.TryGetValue(username, out sessionKey) || sessionKey == null)
				{
					Console.WriteLine("{Client.cs SendTo} Session key not found for " + username);
					return;
				}

				_aesEncryption = new AesEncryption(sessionKey);
				
				var (prefix, payload) = MessageUtils.MessageToBytes(message); // vvv convert the message to bytes
				var encryptedPayload = _aesEncryption!.Encrypt(Encoding.UTF8.GetString(payload)); // encrypt the message
				var signature = _messageSigner!.SignData(encryptedPayload); // sign the message

				var encryptedMessage = new Message
				{
					Type = MessageType.Text,
					Sender = _username,
					TargetPeerId = username,
					RoomName = message.RoomName,
					EncryptedContent = encryptedPayload,
					Signature = signature
				};

				Send(encryptedMessage); // send the message
			}
			catch (IOException)
			{
				Console.WriteLine("Connection to server lost");
			}
			catch (NullReferenceException)
			{
				Console.WriteLine("Null reference occurred while sending message");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}

	/// <summary>
	///     Sends a message to all online users.
	/// </summary>
	public async Task Broadcast(Message message)
	{
		if (IsConnected)
		{
			var getUserListMessage = new Message
			{
				Type = MessageType.Instruction,
				Content = "list all users",
				Sender = _username
			}; // building a message to get all users
			_userListReceived = false;
			Send(getUserListMessage);

			while (!_userListReceived) // vvv wait until the member list is received 
			{
				await Task.Delay(10);
			}

			var usernames = _roomMembers!["all"];

			foreach (var user in usernames) // vvv sends the message to each user in the room individually
			{
				SendTo(user, message);
			}
		}
	}
	
	/// <summary>
	///     Sends a message to the users in a chat room by the room's name.
	/// </summary>
	public async Task SendToRoom(string roomName, Message message)
	{
		if (IsConnected)
		{
			var getRoomMembersMessage = new Message
			{
				Type = MessageType.Instruction,
				Content = "list room members",
				Sender = _username,
				RoomName = roomName
			}; // build a message to retrieve the members of the room
			_userListReceived = false;
			message.RoomName = roomName;

			Send(getRoomMembersMessage);

			while (!_userListReceived) // vvv wait until the member list is received
			{
				await Task.Delay(10);
			}

			var usernames = _roomMembers![roomName];

			foreach (var user in usernames) // vvv sends the message to each user in the room individually
			{
				SendTo(user, message);
			}
		}
	}

	// TODO: figure out a better solution
	/// <summary>
	///     Gets the address of the client.
	/// </summary>
	public string getAddress()
	{
		return _client?.Client.RemoteEndPoint?.ToString() ?? string.Empty;
	}

	/// <summary>
	///     Disconnect from the server.
	///     td Cancel the token, close the stream, close the client.
	/// </summary>
	public void Disconnect()
	{
		_cancellationTokenSource?.Cancel();
		_cancellationTokenSource = null;
		_stream?.Close();
		_stream = null;
		_client?.Close();
		_client = null;
	}
}