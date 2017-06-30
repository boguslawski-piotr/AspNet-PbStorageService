using System;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	[System.Serializable]
	public class Repository : ManagedObject
	{
		/// <summary>
		/// Repository identifier.
		/// </summary>
		public string Id { get; set; }

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

		public Repository()
			: base(null)
		{ }

		public Repository(Manager manager)
			: base(manager)
		{ }

		public static Repository New(Manager manager)
		{
			if(manager == null)
				throw new ArgumentNullException(nameof(manager));

			Repository repository = new Repository(manager);

			repository.Id = Tools.CreateGuidEx();

			RsaCryptographer cryptographer = new RsaCryptographer();
			repository._keys = cryptographer.GenerateKeyPair();

			repository.PublicKey = repository._keys.Public;
			repository.PrivateKey = repository._keys.Private;

			// TODO: encrypt repository.PrivateKey

			repository.PrivateKey = Obfuscator.Obfuscate(repository.PrivateKey);

			return repository;
		}

		public void InitializeAfterDeserialize(Manager manager)
		{
			Manager = manager ?? throw new ArgumentNullException(nameof(manager));

			string privateKey = Obfuscator.DeObfuscate(PrivateKey);

			// TODO: decrypt privateKey

			_keys = new RsaKeyPair(privateKey, PublicKey);
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
