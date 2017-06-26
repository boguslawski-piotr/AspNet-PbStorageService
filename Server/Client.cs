using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	[System.Serializable]
	public class Client : ManagedObject
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
			if(manager == null)
				throw new ArgumentNullException(nameof(manager));

			Client client = new Client(manager);

			client.Id = Tools.CreateGuidEx();

			RsaCryptographer cryptographer = new RsaCryptographer();
			client._keys = cryptographer.GenerateKeyPair();

			client.PublicKey = client._keys.Public;
			client.PrivateKey = client._keys.Private;
			
			// TODO: encrypt & obfuscate client.PrivateKey

			return client;
		}

		public Task InitializeAfterDeserializeAsync(Manager manager)
		{
			Manager = manager ?? throw new ArgumentNullException(nameof(manager));

			// TODO: deobfuscate & decrypt PrivateKey - only for use in RsaKeyPair
			string privateKey = PrivateKey;

			_keys = new RsaKeyPair(privateKey, PublicKey);

			return Task.FromResult(true);
		}

		public string GetIdAndPublicKey()
		{
			return $"{Id},{PublicKey}";
		}

		public string Sign(string data)
		{
			return RsaCryptographerHelper.Sign(data, _keys);
		}

		public string Decrypt(string data)
		{
			return RsaCryptographerHelper.Decrypt(data, _keys);
		}
	}
}
