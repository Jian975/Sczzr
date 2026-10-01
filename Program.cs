// [Your Name Here]
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

namespace SecureMessenger;

internal class Program
{
	// TODO: Uncomment these and use them
	// private static Server? _server;
	// private static Client? _client;
	// private static ConsoleUI? _ui;
	// private static string _username = "User";

	private static async Task Main(string[] args)
	{
		Console.WriteLine("Secure Distributed Messenger");
		Console.WriteLine("============================");

		// TODO: Create your Server, Client, and ConsoleUI instances
		//
		// TODO: Subscribe to events so you know when things happen.
		// For example:
		//   _server.OnClientConnected += (endpoint) => { ... };
		//   _server.OnMessageReceived += (endpoint, message) => { /* relay it */ };
		//   _client.OnMessageReceived += (message) => { /* display it */ };
		//
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

			// Placeholder - replace with your ParseCommand logic
			switch (input.ToLower())
			{
				case "/quit":
				case "/exit":
					running = false;
					break;
				case "/help":
					ShowHelp();
					break;
				default:
					Console.WriteLine("Not yet implemented. See the TODO comments.");
					break;
			}
		}

		// TODO: Clean shutdown - stop the server, disconnect the client

		Console.WriteLine("Goodbye!");
	}

	// Placeholder help - replace with ConsoleUI.ShowHelp() once you've implemented it
	private static void ShowHelp()
	{
		Console.WriteLine("\nCommands:");
		Console.WriteLine("  /connect <ip> <port>  - Connect to a server");
		Console.WriteLine("  /listen <port>        - Start listening for connections");
		Console.WriteLine("  /peers                - Show connection status");
		Console.WriteLine("  /quit                 - Exit");
		Console.WriteLine();
	}

	// TODO: You'll want helper methods like:
	//   HandleListen(args)  - start the server
	//   HandleConnect(args) - connect to a server
	//   HandlePeers()       - print connection info
	//   SendMessage(text)   - create a Message and send via _client.Send()
}