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

		[NonSerialized]
		public DateTime AccesedOn;

		public static async Task<Repository> NewAsync(Context ctx, string storageId, string name)
		{
			Repository repository = new Repository()
			{
				Id = Tools.CreateGuidEx(),
				Name = name,
				_keys = new RsaCryptographer().GenerateKeyPair()
			};

			repository.PublicKey = repository._keys.Public;
			repository.PrivateKey = Obfuscator.Obfuscate(repository._keys.Private);

			string d = ctx.Serializer.Serialize(repository);

			d = Obfuscator.Obfuscate(d);
			if (ctx.Cryptographer != null)
				d = ctx.Cryptographer.Encrypt(d);

			await ctx.RepositoriesDb.StoreThingAsync(storageId, repository.Id, d, DateTime.UtcNow, ctx.Cryptographer).ConfigureAwait(false);

			repository.AccesedOn = DateTime.Now;
			return repository;
		}

		public static async Task<Repository> LoadAsync(Context ctx, string storageId, string id)
		{
			string d = await ctx.RepositoriesDb.GetThingCopyAsync(storageId, id, ctx.Cryptographer).ConfigureAwait(false);

			if (ctx.Cryptographer != null)
				d = ctx.Cryptographer.Decrypt(d);
			d = Obfuscator.DeObfuscate(d);

			Repository repository = ctx.Serializer.Deserialize<Repository>(d);

			repository._keys = new RsaKeyPair(Obfuscator.DeObfuscate(repository.PrivateKey), repository.PublicKey);

			repository.AccesedOn = DateTime.Now;
			return repository;
		}

		public static async Task RemoveAsync(Context ctx, string storageId, string id)
		{
			await ctx.RepositoriesDb.DiscardAllAsync(id);
			await ctx.RepositoriesDb.DiscardThingAsync(storageId, id);
		}

		public string Sign(string data)
		{
			return RsaCryptographerHelper.Sign(data, _keys);
		}

		public string Decrypt(string data)
		{
			return RsaCryptographerHelper.Decrypt(data, _keys);
		}

		public async Task<IEnumerable<IdInDb>> FindIdsAsync(Context ctx, string pattern)
		{
			return await ctx.RepositoriesDb.FindAllIdsAsync(Id, pattern);
		}
	}
}
