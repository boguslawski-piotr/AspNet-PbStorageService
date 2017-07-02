using System;
using pbXNet;

namespace pbXStorage.Server
{
	public class App
	{
		public Repository Repository { get; }

		public string Token { get; }

		/// <summary>
		/// Encrypted (with Repository.PublicKey) app public key sent via net from app to server.
		/// </summary>
		public string PublicKey { get; }

		// Decrypted app public key used to encrypt data which will be send to app from server.
		IAsymmetricCryptographerKeyPair _publicKey;

		public App(Manager manager, Repository repository, string publicKey)
		{
			Repository = repository ?? throw new ArgumentNullException(nameof(repository));
			PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));

			Token = Tools.CreateGuidEx();

			publicKey = Repository.Decrypt(PublicKey);
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
