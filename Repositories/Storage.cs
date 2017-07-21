using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Repositories
{
	public class Storage
	{
		public App App { get; }

		public string Id { get; }

		public string Token { get; }

		IAsymmetricCryptographerKeyPair _keys;

		[NonSerialized]
		public DateTime AccesedOn;

		public Storage(App app, string id)
		{
			App = app ?? throw new ArgumentNullException(nameof(app));

			Id = id ?? throw new ArgumentNullException(nameof(id));

			Token = Tools.CreateGuidEx();

			_keys = new RsaCryptographer().GenerateKeyPair();

			AccesedOn = DateTime.Now;
		}

		public string TokenAndPublicKey
		{
			get {
				string tokenAndPublicKey = $"{Token},{_keys.Public}";

				tokenAndPublicKey = App.Encrypt(tokenAndPublicKey);
				string signature = App.Repository.Sign(tokenAndPublicKey);

				return $"{signature},{tokenAndPublicKey}";
			}
		}

		public string Sign(string data)
		{
			return RsaCryptographerHelper.Sign(data, _keys);
		}

		public string Decrypt(string data)
		{
			return RsaCryptographerHelper.Decrypt(data, _keys);
		}

		string PrepareThingId(string thingId)
		{
			return Regex.Replace(thingId, "[\\/:*?<>|]", "-");
		}

		string IdForDb => Path.Combine(App.Repository.Id, Id).Replace(Path.DirectorySeparatorChar, '/');

		public async Task StoreAsync(Context ctx, string thingId, string data)
		{
			string[] signatureAndData = data.Split(new char[] { ',' }, 2);
			string signature = signatureAndData[0];
			data = signatureAndData[1];

			if (!App.Verify(data, signature))
				throw new Exception(Localized.T("SOPXS_IncorrectData"));

			data = Decrypt(data);

			string[] modifiedOnAndData = data.Split(new char[] { ',' }, 2);
			DateTime modifiedOn = DateTime.FromBinary(long.Parse(modifiedOnAndData[0]));
			data = modifiedOnAndData[1];

			await ctx.RepositoriesDb.StoreThingAsync(IdForDb, PrepareThingId(thingId), data, modifiedOn.ToUniversalTime(), ctx.Cryptographer).ConfigureAwait(false);
		}

		public async Task<string> ExistsAsync(Context ctx, string thingId)
		{
			bool exists = await ctx.RepositoriesDb.ThingExistsAsync(IdForDb, PrepareThingId(thingId)).ConfigureAwait(false);
			return exists ? "YES" : "NO";
		}

		public async Task<string> GetModifiedOnAsync(Context ctx, string thingId)
		{
			thingId = PrepareThingId(thingId);

			if(!await ctx.RepositoriesDb.ThingExistsAsync(IdForDb, thingId).ConfigureAwait(false))
				throw new StorageThingNotFoundException(thingId);

			DateTime modifiedOn = await ctx.RepositoriesDb.GetThingModifiedOnAsync(IdForDb, thingId).ConfigureAwait(false);
			return modifiedOn.ToUniversalTime().ToBinary().ToString();
		}

		public async Task<string> GetACopyAsync(Context ctx, string thingId)
		{
			thingId = PrepareThingId(thingId);

			if (!await ctx.RepositoriesDb.ThingExistsAsync(IdForDb, thingId).ConfigureAwait(false))
				throw new StorageThingNotFoundException(thingId);

			string data = await ctx.RepositoriesDb.GetThingCopyAsync(IdForDb, thingId, ctx.Cryptographer).ConfigureAwait(false);
			DateTime modifiedOn = await ctx.RepositoriesDb.GetThingModifiedOnAsync(IdForDb, thingId).ConfigureAwait(false);

			data = $"{modifiedOn.ToUniversalTime().ToBinary().ToString()},{data}";

			data = App.Encrypt(data);
			string signature = Sign(data);

			return $"{signature},{data}";
		}

		public async Task DiscardAsync(Context ctx, string thingId)
		{
			await ctx.RepositoriesDb.DiscardThingAsync(IdForDb, PrepareThingId(thingId)).ConfigureAwait(false);
		}

		public async Task<string> FindIdsAsync(Context ctx, string pattern)
		{
			if (string.IsNullOrWhiteSpace(pattern))
				pattern = "";

			IEnumerable<IdInDb> ids = await ctx.RepositoriesDb.FindThingIdsAsync(IdForDb, pattern).ConfigureAwait(false);

			StringBuilder sids = null;
			foreach (var id in ids)
			{
				if (id.Type == IdInDbType.Thing)
				{
					if (sids == null)
						sids = new StringBuilder();
					else
						sids.Append('|');
					sids.Append(id.Id);
				}
			}

			string data = sids?.ToString();
			if (data != null)
			{
				data = App.Encrypt(data);
				string signature = Sign(data);

				return $"{signature},{data}";
			}

			return null;
		}
	}
}
