using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class Storage : ManagedObject
	{
		public App App { get; }

		public string Id { get; }

		public string Token { get; }

		IAsymmetricCryptographerKeyPair _keys;

		public Storage(Manager manager, App app, string id)
			: base(manager)
		{
			App = app ?? throw new ArgumentNullException(nameof(app));
			Id = id ?? throw new ArgumentNullException(nameof(id));

			Token = Tools.CreateGuidEx();

			RsaCryptographer cryptographer = new RsaCryptographer();
			_keys = cryptographer.GenerateKeyPair();
		}

		public string TokenAndPublicKey
		{
			get {
				string tokenAndPublicKey = $"{Token},{_keys.Public}";

				tokenAndPublicKey = App.Encrypt(tokenAndPublicKey);
				string signature = App.Client.Sign(tokenAndPublicKey);

				return $"{signature},{tokenAndPublicKey}";
			}
		}

		public string IdForDb => Path.Combine(App.Client.Id, Id);

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

		public async Task StoreAsync(string thingId, string data)
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

			await Manager.Db.StoreThingAsync(IdForDb, thingId, data).ConfigureAwait(false);
			await Manager.Db.SetThingModifiedOnAsync(IdForDb, thingId, modifiedOn.ToUniversalTime()).ConfigureAwait(false);
		}

		public async Task<string> ExistsAsync(string thingId)
		{
			bool exists = await Manager.Db.ThingExistsAsync(IdForDb, thingId).ConfigureAwait(false);
			return exists ? "YES" : "NO";
		}

		public async Task<string> GetModifiedOnAsync(string thingId)
		{
			DateTime modifiedOn = await Manager.Db.GetThingModifiedOnAsync(IdForDb, thingId).ConfigureAwait(false);
			return modifiedOn.ToUniversalTime().ToBinary().ToString();
		}

		public async Task<string> GetACopyAsync(string thingId)
		{
			string data = await Manager.Db.GetThingCopyAsync(IdForDb, thingId).ConfigureAwait(false);
			DateTime modifiedOn = await Manager.Db.GetThingModifiedOnAsync(IdForDb, thingId).ConfigureAwait(false);

			data = $"{modifiedOn.ToUniversalTime().ToBinary().ToString()},{data}";

			data = App.Encrypt(data);
			string signature = Sign(data);

			return $"{signature},{data}";
		}

		public async Task DiscardAsync(string thingId)
		{
			await Manager.Db.DiscardThingAsync(IdForDb, thingId).ConfigureAwait(false);
		}

		public async Task<string> FindIdsAsync(string pattern)
		{
			if (string.IsNullOrWhiteSpace(pattern))
				pattern = "";

			IEnumerable<string> ids = await Manager.Db.FindThingIdsAsync(IdForDb, pattern).ConfigureAwait(false);

			StringBuilder sids = null;
			foreach (var id in ids)
			{
				if (sids == null)
					sids = new StringBuilder();
				else
					sids.Append('|');
				sids.Append(id);
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
