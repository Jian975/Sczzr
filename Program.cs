// Xuejian Sundvall (sprint 1), Athena Ching + Xuejian Sundvall (sprint 2)
// CSCI 251 - Secure Distributed Messenger
//
// Program.cs - Entry point. Sets up the server/client, wires up events,
// and runs the main input loop.
//
// Threading overview:
//   - Main thread: reads console input, parses commands, sends messages
//   - Server accept task: runs in background, accepts incoming connections
//   - Receive tasks: one per connection, reads incoming messages
//
// See HINTS.md for how events, threading, and TCP work together.

using SecureMessenger.Core;
using SecureMessenger.Network;
using SecureMessenger.UI;

namespace SecureMessenger;

internal class Program
{
	// TODO: Uncomment these and use them
	private static Server? _server;

	private static Client? _client;
	// private static ConsoleUI? _ui;

	private static ConsoleUI? _consoleUI;

	//this wasn't in the original code, but I added it to make the peer discovery work:
	private static PeerDiscovery? _peerDiscovery;


	private static async Task Main(string[] args)
	{
		Console.WriteLine("Secure Distributed Messenger");
		Console.WriteLine("============================");

		// TODO: Create your Server, Client, and ConsoleUI instances

		_server = new Server();
		_client = new Client();
		_consoleUI = new ConsoleUI();
		_peerDiscovery = new PeerDiscovery();


		// TODO: Subscribe to events so you know when things happen.
		// For example:
		//   _server.OnClientConnected += (endpoint) => { ... };
		//   _server.OnMessageReceived += (endpoint, message) => { /* relay it */ };
		//   _client.OnMessageReceived += (message) => { /* display it */ };
		_server!.OnClientConnected += endpoint => { _consoleUI.DisplaySystem($"Client connected: {endpoint}"); };
		_server!.OnClientDisconnected += endpoint =>
		{
			_consoleUI.DisplaySystem($"Client disconnected: {endpoint}");
			_server!.RemoveUsername(endpoint);
		};
		_server!.OnMessageReceived += (endpoint, message) =>
		{
			_consoleUI.DisplayMessage(message);
			if (message.Type == MessageType.KeyExchange)
			{
				// Add the public key to the server's list of known public keys
				_server!.AddPublicKey(message.Sender, message.PublicKey!);
				// Add the username to the server's list of known usernames and endpoints
				_server!.AddUsername(message.Sender, endpoint);
				// Broadcast the list of public keys to all clients
				_server!.BroadcastPublicKeys();
			}
			else if (message.Type == MessageType.SessionKey)
			{
				//Forward the session key to the intended recipient
				if (message.TargetPeerId == null || message.TargetPeerId == "" ||
				    _server!.GetEndpointByUsername(message.TargetPeerId!) == null)
				{
					_consoleUI.DisplaySystem($"SessionKey message TargetPeerId missing or unknown: {message}");
					return;
				}

				_server!.SendTo(_server!.GetEndpointByUsername(message.TargetPeerId!)!, message);
			}
			else if (message.Type == MessageType.Instruction)
			{
				// Handle room creation, joining, leaving, and listing commands
				switch (message.Content)
				{
					case "create":
						_server!.CreateRoom(message.RoomName!);
						break;
					case "join":
						_server!.AddToRoom(message.Sender, message.RoomName!);
						break;
					case "leave":
						_server!.RemoveFromRoom(message.Sender, message.RoomName!);
						break;
					case "list rooms":
						var rooms = _server!.ListRooms();
						var roomListMessage = new Message
						{
							Sender = "Server",
							Content = string.Join(", ", rooms),
							Type = MessageType.Text,
							Timestamp = DateTime.Now,
							Request = "list rooms"
						};
						_server!.SendTo(endpoint, roomListMessage);
						break;
					case "list room members":
						var members = _server!.GetRoomMembers(message.RoomName!);
						var memberListMessage = new Message
						{
							Sender = "Server",
							Content = members,
							Type = MessageType.Text,
							Timestamp = DateTime.Now,
							RoomName = message.RoomName,
							Request = "list room members"
						};
						_server!.SendTo(endpoint, memberListMessage);
						break;
					case "list all users":
						var allUsers = _server!.GetAllUsers();
						var allUsersMessage = new Message
						{
							Sender = "Server",
							Content = allUsers,
							Type = MessageType.Text,
							Timestamp = DateTime.Now,
							Request = "list all users"
						};
						_server!.SendTo(endpoint, allUsersMessage);
						break;
					default:
						_consoleUI.DisplaySystem($"Unknown instruction: {message.Content}");
						break;
				}
			}
			else if (message.Type == MessageType.Text && !string.IsNullOrEmpty(message.TargetPeerId))
			{
				if (!string.IsNullOrEmpty(message.RoomName) &&
				    (!_server!.IsRoomMember(message.Sender, message.RoomName) ||
				     !_server.IsRoomMember(message.TargetPeerId, message.RoomName)))
				{
					_consoleUI.DisplaySystem(
						$"Rejected room message from {message.Sender} to {message.TargetPeerId}: not a room member");
					return;
				}

				var recipientEndpoint = _server!.GetEndpointByUsername(message.TargetPeerId);
				if (recipientEndpoint != null)
				{
					_server.SendTo(recipientEndpoint, message);
				}
			}
			else
			{
				_server!.Broadcast(message);
			}
		};
		_client!.OnConnected += endpoint => _consoleUI.DisplaySystem($"Connected to server: {endpoint}");
		_client!.OnDisconnected += endpoint => _consoleUI.DisplaySystem($"Disconnected from server: {endpoint}");
		_client!.OnMessageReceived += message => _consoleUI.DisplayMessage(message);
		// In Sprint 1, the server is a pure relay: when it receives a message,
		// broadcast it to every connected client with Broadcast(message). Starting
		// in Sprint 2 (chat rooms) and Sprint 3 (direct peer messages), you won't
		// always want everyone to get it - use SendTo(endpoint, message) to send
		// to just one client instead. That's why OnMessageReceived hands you the
		// sending endpoint: you'll need it to decide who else should receive it.
		//
		// Nothing stops one process from handling both /listen and /connect - _server
		// and _client are independent objects. For Sprint 1 and 2, don't: submit one
		// relay process (/listen only) plus separate client processes (/connect only),
		// per the spec. Sprint 3 flips this - a peer runs both at once on purpose.

		Console.WriteLine("Type /help for available commands");
		Console.WriteLine();

		// Main input loop
		var running = true;
		while (running)
		{
			// TODO: Replace this with your full implementation.
			// Read input, parse it with ConsoleUI.ParseCommand(), then
			// handle the result:
			//   Listen  -> _server.Start(port)
			//   Connect -> await _client.ConnectAsync(host, port)
			//   Peers   -> show connection status
			//   Quit    -> set running = false
			//   Not a command -> send it as a chat message

			var input = Console.ReadLine();
			if (string.IsNullOrEmpty(input))
			{
				continue;
			}

			var inputCmd = _consoleUI.ParseCommand(input);
			if (inputCmd.IsCommand)
			{
				switch (inputCmd.CommandType)
				{
					case CommandType.Connect:
						if (_client!.IsConnected)
						{
							Console.WriteLine("Already connected to a server.");
						}
						else
						{
							await HandleConnect(inputCmd.Args!);
						}

						break;
					case CommandType.Listen:
						if (_server!.IsListening)
						{
							Console.WriteLine("Server is already listening.");
						}
						else
						{
							HandleListen(inputCmd.Args!);
						}

						break;
					case CommandType.Peers:
						HandlePeers();
						break;
					case CommandType.Username:
						if (inputCmd.Args![0] == "Server")
						{
							Console.WriteLine("Username cannot be 'Server'.");
						}
						else
						{
							_client!._username = inputCmd.Args![0].Trim();
						}

						break;
					case CommandType.Quit:
						running = false;
						break;
					case CommandType.Help:
						_consoleUI.ShowHelp();
						break;
					case CommandType.Create:
						CreateRoom(inputCmd.Args![0]);
						break;
					case CommandType.Join:
						AddToRoom(inputCmd.Args![0], _client.getAddress());
						break;
					case CommandType.Leave:
						RemoveFromRoom(inputCmd.Args![0], _client.getAddress()); // Handle leave command
						break;
					case CommandType.Rooms:
						ListRooms();
						break;
					case CommandType.RoomMembers:
						ListMembers(inputCmd.Args![0]);
						break;
					case CommandType.MessageRoom:
						if (_client!.IsConnected)
						{
							SendToRoom(inputCmd.Args![0], inputCmd.Args![1]);
						}
						else
						{
							Console.WriteLine("Not connected to a server. Use /connect <ip> <port> to connect.");
						}

						break;
					case CommandType.Unknown:
					default:
						Console.WriteLine("Unknown command. Type /help for available commands.");
						break;
				}
			}
			else
			{
				// send as a chat message
				if (_client!.IsConnected)
				{
					Broadcast(new Message
					{
						Content = input, Sender = _client!._username, Type = MessageType.Text, Timestamp = DateTime.Now
					});
				}
				else
				{
					Console.WriteLine("Not connected to a server. Use /connect <ip> <port> to connect.");
				}
			}
			// Placeholder - replace with your ParseCommand logic
		}

		// Clean shutdown - stop the server, disconnect the client
		if (_server!.IsListening)
		{
			_server.Stop();
		}

		if (_client!.IsConnected)
		{
			_client.Disconnect();
		}

		_consoleUI.DisplaySystem("Goodbye!");
	}

	private static async Task HandleConnect(string[] args)
	{
		await _client!.ConnectAsync(args[0], int.Parse(args[1]));
	}

	private static void HandleListen(string[] args)
	{
		_server!.Start(int.Parse(args[0]));
	}

	private static void HandlePeers()
	{
		var peers = _peerDiscovery!.GetKnownPeers().ToList();

		if (peers.Count == 0)
		{
			Console.WriteLine("No known peers.");
		}
		else
		{
			foreach (var peer in peers)
			{
				Console.WriteLine(peer.Id);
			}
		}
	}

	private static void SendMessage(string text)
	{
		_client!.Send(new Message
			{Content = text, Sender = _client!._username, Type = MessageType.Text, Timestamp = DateTime.Now});
	}

	//added in sprint 2:

	//create
	private static void CreateRoom(string roomName)
	{
		_client!.Send(new Message
		{
			Content = "create", Sender = _client!._username, Type = MessageType.Instruction, Timestamp = DateTime.Now,
			RoomName = roomName
		});
	}

	//join
	private static void AddToRoom(string roomName, string endpoint)
	{
		_client!.Send(new Message
		{
			Content = "join", Sender = _client!._username, Type = MessageType.Instruction, Timestamp = DateTime.Now,
			RoomName = roomName
		});
	}

	//leave
	private static void RemoveFromRoom(string roomName, string endpoint)
	{
		_client!.Send(new Message
		{
			Content = "leave", Sender = _client!._username, Type = MessageType.Instruction, Timestamp = DateTime.Now,
			RoomName = roomName
		});
	}

	//rooms
	private static void ListRooms()
	{
		_client!.Send(new Message
		{
			Content = "list rooms", Sender = _client!._username, Type = MessageType.Instruction,
			Timestamp = DateTime.Now
		});
	}

	//members
	private static void ListMembers(string roomName)
	{
		_client!.Send(new Message
		{
			Content = "list room members", Sender = _client!._username, Type = MessageType.Instruction,
			Timestamp = DateTime.Now, RoomName = roomName
		});
	}

	//msg
	//TODO: set security fields
	private static void SendToRoom(string roomName, string text)
	{
		_client!.SendToRoom(roomName,
			new Message
			{
				Content = text, Sender = _client!._username, Type = MessageType.Text, Timestamp = DateTime.Now,
				RoomName = roomName
			});
	}

	private static void Broadcast(Message message)
	{
		if (_client!.IsConnected)
		{
			_client.Broadcast(message);
		}
		else
		{
			Console.WriteLine("Not connected to a server. Use /connect <ip> <port> to connect.");
		}
	}
}