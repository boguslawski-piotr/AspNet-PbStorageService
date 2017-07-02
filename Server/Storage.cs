using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class Storage
	{
		public App App { get; }

		public string Id { get; }

		public string Token { get; }

		IAsymmetricCryptographerKeyPair _keys;

		public Storage(App app, string id)
		{
			App = app ?? throw new ArgumentNullException(nameof(app));

			Id = id ?? throw new ArgumentNullException(nameof(id));

			Token = Tools.CreateGuidEx();

			_keys = new RsaCryptographer().GenerateKeyPair();
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

		public string IdForDb => Path.Combine(App.Repository.Id, Id).Replace(Path.DirectorySeparatorChar, '/');

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

		public async Task StoreAsync(IDb db, string thingId, string data)
		{
			string[] signatureAndData = data.Split(new char[] { ',' }, 2);
			string signature = signatureAndData[0];
			data = signatureAndData[1];

			if (!App.Verify(data, signature))
				throw new Exception("Incorrect data.");

			data = Decrypt(data);

			string[] modifiedOnAndData = data.Split(new char[] { ',' }, 2);
			DateTime modifiedOn = DateTime.FromBinary(long.Parse(modifiedOnAndData[0]));
			data = modifiedOnAndData[1];

			await db.StoreThingAsync(IdForDb, thingId, data, modifiedOn.ToUniversalTime()).ConfigureAwait(false);
		}

		public async Task<string> ExistsAsync(IDb db, string thingId)
		{
			bool exists = await db.ThingExistsAsync(IdForDb, thingId).ConfigureAwait(false);
			return exists ? "YES" : "NO";
		}

		public async Task<string> GetModifiedOnAsync(IDb db, string thingId)
		{
			DateTime modifiedOn = await db.GetThingModifiedOnAsync(IdForDb, thingId).ConfigureAwait(false);
			return modifiedOn.ToUniversalTime().ToBinary().ToString();
		}

		public async Task<string> GetACopyAsync(IDb db, string thingId)
		{
			string data = await db.GetThingCopyAsync(IdForDb, thingId).ConfigureAwait(false);
			DateTime modifiedOn = await db.GetThingModifiedOnAsync(IdForDb, thingId).ConfigureAwait(false);

			data = $"{modifiedOn.ToUniversalTime().ToBinary().ToString()},{data}";

			data = App.Encrypt(data);
			string signature = Sign(data);

			return $"{signature},{data}";
		}

		public async Task DiscardAsync(IDb db, string thingId)
		{
			await db.DiscardThingAsync(IdForDb, thingId).ConfigureAwait(false);
		}

		public async Task<string> FindIdsAsync(IDb db, string pattern)
		{
			if (string.IsNullOrWhiteSpace(pattern))
				pattern = "";

			IEnumerable<IdInDb> ids = await db.FindThingIdsAsync(IdForDb, pattern).ConfigureAwait(false);

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
