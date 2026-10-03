// Jackson Welde
// CSCI 251 - Secure Distributed Messenger
//
// MessageUtils.cs - Utility class with methods for handling and converting message bytes.

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SecureMessenger.Core;

namespace SecureMessenger;

public class MessageUtils
{
	/// <summary>
	///     Converts the inputted message to length and payload bytes.
	/// </summary>
	public static (byte[] lengthBytes, byte[] payload) messageToBytes(Message message)
	{
		string json = JsonSerializer.Serialize(message);
		byte[] payload = Encoding.UTF8.GetBytes(json);

		byte[] lengthBytes = BitConverter.GetBytes(payload.Length);

		return (lengthBytes, payload);
	}

	/// <summary>
	///     Converts the inputted payload bytes into a message.
	/// </summary>
	public static Message payloadToMessage(byte[] payload)
	{
		string json = Encoding.UTF8.GetString(payload);
		Message? message = JsonSerializer.Deserialize<Message>(json);

		return message;
	}

	/// <summary>
	///     Reads the specified length bytes from the stream until all are received.
	/// </summary>
	public static async Task<byte[]> ReadBytesAsync(NetworkStream stream, int length, CancellationToken token)
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
				throw new IOException("Disconnected.");
			}

			bytesRead += read;
		}

		return buffer;
	}

	/// <summary>
	///     Converts the inputted length bytes into an integer length.
	/// </summary>
	public static int lengthBytesToLength(byte[] lengthBytes)
	{
		return BitConverter.ToInt32(lengthBytes, 0);
	}

	private const int MEGABYTE_LENGTH = 1048576;

	/// <summary>
	///     Checks if the inputted value is valid for a message length.
	///     True if valid; false else.
	/// </summary>
	public static bool isValidMessageLength(int length)
	{
		return length is > 0 and < MEGABYTE_LENGTH;
	}
}