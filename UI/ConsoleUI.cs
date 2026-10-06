// Lydia Barriga
// CSCI 251 - Secure Distributed Messenger
//
// ConsoleUI.cs - Handles command parsing and message display.

using Microsoft.VisualBasic;
using SecureMessenger.Core;

namespace SecureMessenger.UI;

/// <summary>
///     Console user interface. Parses input and displays messages.
///     Commands: /connect, /listen, /peers, /quit, /help
///     Anything else is treated as a chat message.
/// </summary>
public class ConsoleUI
{
	/// <summary>
	///     Display a message in the console.
	///     TODO: Format it nicely with a timestamp and sender name.
	///     Something like: [14:30:25] Alice: Hello!
	///     (message.Timestamp.ToString("HH:mm:ss") for the time)
	/// </summary>
	public void DisplayMessage(Message message)
	{	
		String name = message.Sender;
		String date = message.Timestamp.ToString("HH:mm:ss");
		String content = message.Content;
		String msg = $"[{date}] {name}: {content}";
		Console.WriteLine(msg);
		// throw new NotImplementedException("Implement DisplayMessage()");
	}

	/// <summary>
	///     Display a system notification (not a chat message).
	///     TODO: Print with some prefix like [System] so it's visually distinct.
	/// </summary>
	public void DisplaySystem(string message)
	{
		String date = DateTime.Now.ToString("HH:mm:ss");
		String msg = $"[{date}] *SYSTEM*: {message}";
		Console.WriteLine(msg);
		//throw new NotImplementedException("Implement DisplaySystem()");
	}

	/// <summary>
	///     Print available commands.
	/// </summary>
	public void ShowHelp()
	{
		DisplaySystem("Hello! Here is a handy list of our commands:\n/connect <host> <port>\n/listen <port>\n/peers\n/quit -quits\n/exit\n/help");
		// throw new NotImplementedException("Implement ShowHelp()");
	}

	/// <summary>
	///     Parse a line of user input into either a command or a chat message.
	///     TODO:
	///     If input doesn't start with "/", it's a message - return a CommandResult
	///     with IsCommand = false and Message = the input text.
	///     If it does start with "/", split on spaces and figure out which command:
	///     /connect host port  -> CommandType.Connect, Args = [host, port]
	///     /listen port        -> CommandType.Listen, Args = [port]
	///     /peers              -> CommandType.Peers
	///     /quit or /exit      -> CommandType.Quit
	///     /help               -> CommandType.Help
	///     anything else       -> CommandType.Unknown with an error message
	///     Check that /connect has 2 args and /listen has 1. If not, return
	///     Unknown with a usage hint.
	///     Tip: input.Split(' ', StringSplitOptions.RemoveEmptyEntries) and
	///     a switch on parts[0].ToLower() works well here.
	/// </summary>
	public CommandResult ParseCommand(string input)
	{
		if (input.StartsWith("/"))
		{
			String[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			switch (parts[0].ToLower())
			{
				case "":
				default:
					break;
			}
		}
		//throw new NotImplementedException("Implement ParseCommand()");
	}
}

public enum CommandType
{
	Unknown,
	Connect,
	Listen,
	Peers,
	History,
	Help,
	Quit
}

/// <summary>
///     What ParseCommand returns. Either a command (IsCommand = true) with
///     a CommandType and optional Args, or a chat message (IsCommand = false)
///     with the text in Message.
/// </summary>
public class CommandResult
{
	public bool IsCommand { get; set; }
	public CommandType CommandType { get; set; }
	public string[]? Args { get; set; }
	public string? Message { get; set; }
}