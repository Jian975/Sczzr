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
using SecureMessenger.Core;
using SecureMessenger.Security;

namespace SecureMessenger.Network;

/// <summary>
///     TCP client - connects to a server, sends messages, receives messages.
/// </summary>
public class Client
{
	private CancellationTokenSource? _cancellationTokenSource;
	private TcpClient? _client;
	private string _serverEndpoint = "";
	private NetworkStream? _stream;
	public bool IsConnected => _client?.Connected ?? false;
	public event Action<string>? OnConnected;
	public event Action<string>? OnDisconnected;
	public event Action<Message>? OnMessageReceived;

	// Peer ID (or Endpoint) -> Shared AES Key/IV
	private readonly ConcurrentDictionary<string, AesEncryption> _sessionKeys = new ConcurrentDictionary<string, AesEncryption>();

	private MessageSigner _messageSigner;

	private byte[] _publicKey {get; set;}

	private RSA _rsa {get; set;}

	private RsaEncryption rsaEncryption {get; set;}

	private ConcurrentDictionary<string, byte[]> _publicKeys = new ConcurrentDictionary<string, byte[]>();

	public string _username { get; private set; } = "";	//TODO: move username storage from Program to Client during sprint 3

    /// <summary> 
    ///     If the connection is successful, it initializes the network stream, 
    ///     saves the server endpoint, fires the OnConnected event, 
    ///     and starts receiving messages in a background task. 
    ///     If any exceptions occur during the connection process, 
    ///     they are caught and logged, and the method returns false.
    ///     @param host The server's hostname or IP address.
    ///     @param port The server's port number.
    ///     @return True if the connection is successful, false otherwise.
    ///     
    /// td:
    ///     Create a CancellationTokenSource and a new TcpClient, then
    ///     await ConnectAsync(host, port). Grab the NetworkStream, save the
    ///     endpoint string, fire OnConnected, and start ReceiveAsync on a
    ///     background Task. Return true.
    ///     If anything throws, log the error and return false.
    /// 
	/// TODO: add key exchange (call to KeyExchange) for rsa public keys
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

			_rsa = RSA.Create();
			_publicKey = _rsa.ExportRSAPublicKey(); //get public key bytes
			
			rsaEncryption = new RsaEncryption();
			rsaEncryption.ImportPublicKey(_publicKey); //import the public key into the RSA encryption instance
			
			_messageSigner = new MessageSigner(_rsa); //create message signer with the RSA instance

			//todo: uncomment these lines once encryption doesn't throw an not implemented err
			//_rsaEncryption = new RsaEncryption();
			//_messageSigner = new MessageSigner(RSA.Create()); //TODO: figure out how to actually instantiate
            // Fire the OnConnected event
            OnConnected?.Invoke(_serverEndpoint);

			//send public key to server
			Message keyExchange = new Message()
			{
				PublicKey = _publicKey,
				Type = MessageType.KeyExchange,
				Timestamp = DateTime.Now
			};
			Send(keyExchange);
			

            // Start ReceiveAsync on a background Task
            _ = Task.Run(() => ReceiveAsync());
            
			return true;

        }
		catch(SocketException) 
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
		CancellationToken token = _cancellationTokenSource!.Token;
		byte[] buffer = new byte[4];

		var responseBytes = new List<byte>();
		try
		{
			while (IsConnected && !token.IsCancellationRequested)
			{
                // Read the 4-byte length prefix
                byte[] prefix = await MessageUtils.ReadBytesAsync(_stream!, 4, token);

                // Convert the length bytes to an integer
                int length = MessageUtils.LengthBytesToLength(prefix);

                // Validate the length (> 0 and < 1MB)
                if (!MessageUtils.IsValidMessageLength(length))
				{
                    break;
				}

				byte[] payload = await MessageUtils.ReadBytesAsync(_stream!, length, token);
				
				// Deserialize the JSON into a Message
				Message message = MessageUtils.PayloadToMessage(payload);

                // Fire the OnMessageReceived event
                OnMessageReceived?.Invoke(message);
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

	public async Task SendTo(string endpoint, Message message)
	{
		if (IsConnected)
		{
			try
			{
				if (!_sessionKeys.ContainsKey(endpoint))
				{
					Console.WriteLine("No session key found for the specified endpoint");
					return;
				}

				var (prefix, payload) = MessageUtils.MessageToBytes(message);

				// Encrypt the payload with the receiver's AES key
				var rsaEncryption = new RsaEncryption();
				byte[] rsaPublicKey = rsaEncryption.ExportPublicKey();
				var aes = new AesEncryption(rsaPublicKey);


				//encrypt with receiver's aes key
				byte[] encryptedPayload = aes.Encrypt(Encoding.UTF8.GetString(payload));
				byte[] encryptedPrefix = BitConverter.GetBytes(encryptedPayload.Length);

				byte[] signature = _messageSigner.SignData(encryptedPayload);
				//await _stream!.WriteAsync(encryptedPrefix.Concat(encryptedPayload).Concat(signature).ToArray(), 
				//				0, encryptedPrefix.Length + encryptedPayload.Length + signature.Length);
				await _stream!.WriteAsync(encryptedPrefix.Concat(signature).ToArray(), 
								0, encryptedPrefix.Length + signature.Length);
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

	//TODO: figure out a better solution
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