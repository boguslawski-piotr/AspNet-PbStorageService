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

		[NonSerialized]
		public DateTime AccesedOn;

		public App(Repository repository, string publicKey)
		{
			Repository = repository ?? throw new ArgumentNullException(nameof(repository));
			PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));

			Token = Tools.CreateGuidEx();

			_publicKey = new RsaKeyPair(null, Repository.Decrypt(PublicKey));

			AccesedOn = DateTime.Now;
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
