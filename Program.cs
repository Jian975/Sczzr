// Xuejian Sundvall
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
	private static string _username = "User";

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
        _server!.OnClientConnected += (endpoint) => _consoleUI.DisplaySystem($"Client connected: {endpoint}");
		_server!.OnClientDisconnected += (endpoint) => _consoleUI.DisplaySystem($"Client disconnected: {endpoint}");
		_server!.OnMessageReceived += (endpoint, message) => {
			_consoleUI.DisplayMessage(message);
			_server!.Broadcast(message);
		};
		_client!.OnConnected += (endpoint) => _consoleUI.DisplaySystem($"Connected to server: {endpoint}");
		_client!.OnDisconnected += (endpoint) => _consoleUI.DisplaySystem($"Disconnected from server: {endpoint}");
		_client!.OnMessageReceived += (message) => _consoleUI.DisplayMessage(message);
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
			
			CommandResult inputCmd = _consoleUI.ParseCommand(input);
			if (inputCmd.IsCommand)
			{
				switch (inputCmd.CommandType)
				{
					case CommandType.Connect:
						HandleConnect(inputCmd.Args);
						break;
					case CommandType.Listen:
						HandleListen(inputCmd.Args);
						break;
					case CommandType.Peers:
						HandlePeers();
						break;
					case CommandType.Username:
						_username = inputCmd.Args[0];
						break;
					case CommandType.Quit:
						running = false;
						break;
					case CommandType.Help:
						_consoleUI.ShowHelp();
						break;
					case CommandType.Unknown:
					default:
						break;
				}
			}
			else
			{
				// send as a chat message
				if (_client!.IsConnected) {
					SendMessage(input);
				} else {
					Console.WriteLine("Not connected to a server. Use /connect <ip> <port> to connect.");
				}
			}
			// Placeholder - replace with your ParseCommand logic
		}

		// Clean shutdown - stop the server, disconnect the client
		if (_server!.IsListening) {
			_server.Stop();
		}
		if (_client!.IsConnected) {
			_client.Disconnect();
		}
		_consoleUI.DisplaySystem("Goodbye!");
	}

	// Placeholder help - replace with ConsoleUI.ShowHelp() once you've implemented it
	private static void ShowHelp()
	{
		// Console.WriteLine("\nCommands:");
		// Console.WriteLine("  /connect <ip> <port>  - Connect to a server");
		// Console.WriteLine("  /listen <port>        - Start listening for connections");
		// Console.WriteLine("  /peers                - Show connection status");
		// Console.WriteLine("  /username <name>      - Change your username");
		// Console.WriteLine("  /quit                 - Exit");
		// Console.WriteLine("  /exit                 - Exit");
		// Console.WriteLine("  /help                 - Show available commands");
		// Console.WriteLine();
	}

	private async static void HandleConnect(string[] args)
	{
		await _client!.ConnectAsync(args[0], int.Parse(args[1]));
	}

	private static void HandleListen(string[] args)
	{
		_server!.Start(int.Parse(args[0]));
	}

	private static void HandlePeers()
	{
		List<PeerInfo> peers = _peerDiscovery!.GetKnownPeers().ToList();
		
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
		_client!.Send(new Message { Content = text, Sender = _username, Type = MessageType.Text, Timestamp = DateTime.Now, });
	}
}