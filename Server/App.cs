using System;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class App : Base
	{
		public Client Client { get; }

		public string Token { get; }

		/// <summary>
		/// Encrypted (with Client.PublicKey) app public key sent via net from app to server.
		/// </summary>
		public string PublicKey { get; }

		// Decrypted app public key used to encrypt data which will be send to app from server.
		IByteBuffer _publicKey;

		public App(Manager manager, Client client, string publicKey)
			: base(manager)
		{
			Client = client ?? throw new ArgumentNullException(nameof(client));
			PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));

			Token = Tools.CreateGuid();

			publicKey = Client.Decrypt(PublicKey);
			// TODO: try to decrypt publicKey with Client.PrivateKey, throw exception if error
			_publicKey = null;
		}

		public string GetToken()
		{
			string token = Token;
			string signature = Client.Sign(token);

			string data = $"{signature},{token}";
			return Obfuscator.Obfuscate(data);
		}

		public string Encrypt(string data)
		{
			return data;
		}

		public bool Verify(string data, string signature)
		{
			return true;
		}
	}
}
