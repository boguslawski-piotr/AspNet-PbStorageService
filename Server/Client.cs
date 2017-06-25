using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	[System.Serializable]
	public class Client : Base
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
		IAsymmetricCryptographerKeyPair _keys;

		public Client()
			: base(null)
		{ }

		public Client(Manager manager)
			: base(manager)
		{ }

		public static Client New(Manager manager)
		{
			Client client = new Client(manager)
			{
				Id = Tools.CreateGuid(),

				// TODO: create clientKeyPair

				PublicKey = "client public key",
				PrivateKey = "client private key",
			};

			return client;
		}

		public async Task InitializeAfterDeserializeAsync(Manager manager)
		{
			Manager = manager;
			// TODO: deobfuscate/decrypt PublicKey/PrivateKey
		}

		public string GetIdAndPublicKey()
		{
			string data = $"{Id},{PublicKey}";
			return Obfuscator.Obfuscate(data);
		}

		public string Sign(string data)
		{
			return "signature";
		}

		public string Decrypt(string data)
		{
			return data;
		}
	}
}
