using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	[System.Serializable]
	public class Repository
	{
		/// <summary>
		/// Repository identifier.
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// Repository name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Public key as readable string ready to store, to send via net and to pass to RsaKeyPair constructor.
		/// </summary>
		public string PublicKey { get; set; }

		/// <summary>
		/// Obfuscated private key ready to store.
		/// </summary>
		public string PrivateKey { get; set; }

		// Real key pair used to encrypt/decrypt/sign/verify data.
		IAsymmetricCryptographerKeyPair _keys;

		public static Repository New(string name)
		{
			Repository repository = new Repository()
			{
				Id = Tools.CreateGuidEx(),
				Name = name,
				_keys = new RsaCryptographer().GenerateKeyPair()
			};

			repository.PublicKey = repository._keys.Public;
			repository.PrivateKey = Obfuscator.Obfuscate(repository._keys.Private);

			return repository;
		}

		public void InitializeAfterDeserialize()
		{
			string privateKey = Obfuscator.DeObfuscate(PrivateKey);
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

		public async Task<IEnumerable<IdInDb>> FindIdsAsync(IDb db, string pattern)
		{
			return await db.FindAllIdsAsync(Id, pattern);
		}
	}
}
