// Jackson Welde (sprint 2)
// CSCI 251 - Secure Distributed Messenger
//
// SPRINT 2: Security & Encryption
// Due: Week 10
//

using System.Security.Cryptography;

namespace SecureMessenger.Security;

/// <summary>
///     Sprint 2: Message signing and verification.
///     Uses RSA with SHA-256 for digital signatures.
///     Digital Signature Configuration:
///     - Algorithm: RSA with SHA-256
///     - Padding: PKCS#1 v1.5 (RSASignaturePadding.Pkcs1)
///     Purpose:
///     - Signing proves the message came from the claimed sender
///     - Verification detects if the message was tampered with
///     - Reject any message with an invalid signature
/// </summary>
public class MessageSigner
{
	private readonly RSA _rsa;

	/// <summary>
	///     Create a MessageSigner with an RSA key pair.
	///     Use your own RSA instance for signing outgoing messages.
	/// </summary>
	public MessageSigner(RSA rsa)
	{
		_rsa = rsa;
	}

	/// <summary>
	///     Sign data with our private key.
	/// </summary>
	public byte[] SignData(byte[] data)
	{
		return _rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
	}

	/// <summary>
	///     Verify a message signature with the sender's public key.
	/// </summary>
	public bool VerifyData(byte[] data, byte[] signature, byte[] publicKey)
	{
		try
		{
			using var rsa = RSA.Create();
			rsa.ImportRSAPublicKey(publicKey, out _); // get public key

			var isValid = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1); // check if the data was tampered with

			if (!isValid)
			{
				Console.WriteLine("WARNING: Invalid signature detected - message may be tampered!");
			}

			return isValid;
		}
		catch (CryptographicException)
		{
			Console.WriteLine("ERROR: Failed to verify signature - rejecting message");
			return false;
		}
	}
}