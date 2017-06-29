using System;
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
		/// Client role. Only Admins can create new clients.
		/// </summary>
		public bool IsAdmin { get; set; }

		/// <summary>
		/// Public key as readable string ready to store in storage, to send via net and to pass to RsaKeyPair constructor.
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

		public static Client New(Manager manager, bool isAdmin = false)
		{
			if(manager == null)
				throw new ArgumentNullException(nameof(manager));

			Client client = new Client(manager);

			client.Id = Tools.CreateGuidEx();
			client.IsAdmin = isAdmin;

			RsaCryptographer cryptographer = new RsaCryptographer();
			client._keys = cryptographer.GenerateKeyPair();

			client.PublicKey = client._keys.Public;
			client.PrivateKey = client._keys.Private;

			// TODO: encrypt client.PrivateKey

			client.PrivateKey = Obfuscator.Obfuscate(client.PrivateKey);

			return client;
		}

		public void InitializeAfterDeserialize(Manager manager)
		{
			Manager = manager ?? throw new ArgumentNullException(nameof(manager));

			string privateKey = Obfuscator.DeObfuscate(PrivateKey);

			// TODO: decrypt privateKey

			_keys = new RsaKeyPair(privateKey, PublicKey);
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
