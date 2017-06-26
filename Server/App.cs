using System;
using pbXNet;

namespace pbXStorage.Server
{
	public class App : ManagedObject
	{
		public Client Client { get; }

		public string Token { get; }

		/// <summary>
		/// Encrypted (with Client.PublicKey) app public key sent via net from app to server.
		/// </summary>
		public string PublicKey { get; }

		// Decrypted app public key used to encrypt data which will be send to app from server.
		IAsymmetricCryptographerKeyPair _publicKey;

		public App(Manager manager, Client client, string publicKey)
			: base(manager)
		{
			Client = client ?? throw new ArgumentNullException(nameof(client));
			PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));

			Token = Tools.CreateGuidEx();

			publicKey = Client.Decrypt(PublicKey);
			_publicKey = new RsaKeyPair(null, publicKey);
		}

		public string Encrypt(string data)
		{
			return RsaCryptographerHelper.Encrypt(data, _publicKey);
		}

		public bool Verify(string data, string signature)
		{
			return RsaCryptographerHelper.Verify(data, signature, _publicKey);
		}
	}
}
