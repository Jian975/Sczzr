using System.Text;
using System.Text.Json;
using SecureMessenger.Core;

namespace SecureMessenger;

public class MessageUtils
{
	/// <summary>
	///     Converts the inputted message to length and payload bytes.
	/// </summary>
	public (byte[] lengthBytes, byte[] payload) messageToBytes(Message message)
	{
		string json = JsonSerializer.Serialize(message);
		byte[] payload = Encoding.UTF8.GetBytes(json);

		byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
		
		return (lengthBytes, payload);
	}
	
	/// <summary>
	///     Converts the inputted payload bytes into a message.
	/// </summary>
	public Message payloadToMessage(byte[] payload)
	{
		string json = Encoding.UTF8.GetString(payload);
		Message? message = JsonSerializer.Deserialize<Message>(json);
		
		return message;
	}
	
	/// <summary>
	///     Converts the inputted length bytes into an integer length.
	/// </summary>
	public int lengthBytesToLength(byte[] lengthBytes)
	{
		return BitConverter.ToInt32(lengthBytes, 0);
	}

	private const int MEGABYTE_LENGTH = 1048576;
	/// <summary>
	///     Checks if the inputted value is valid for a message length.
	///     True if valid; false else.
	/// </summary>
	public bool isValidMessageLength(int length)
	{
		return length is > 0 and < MEGABYTE_LENGTH;
	}
}