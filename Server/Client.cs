using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class Client
	{
		/// <summary>
		/// Client identifier.
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Public key as readable string ready to store in storage and to send via net.
		/// </summary>
		public string PublicKey { get; set; }

		/// <summary>
		/// Encrypted and obfuscated private key ready to store in storage.
		/// </summary>
		public string PrivateKey { get; set; }

		// Real key pair used to encrypt/decrypt/sign/verify data.
		IByteBuffer _publicKey;
		IByteBuffer _privateKey;

		public static Client New()
		{
			Client client = new Client
			{
				Id = Tools.CreateGuid(),

				// TODO: create clientKeyPair

				PublicKey = "public key",
				PrivateKey = "private key",
			};

			return client;
		}

		public void InitializeAfterDeserialize()
		{
			// TODO: deobfuscate/decrypt PublicKey/PrivateKey
		}

		public string GetIdAndPublicKey()
		{
			return $"{Id},{PublicKey}";
		}
	}
}
