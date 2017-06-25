using System;
using System.Threading;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class Storage : Base
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

			Token = Tools.CreateGuid();

			// TODO: create key pair
			
		}

		public string GetTokenAndPublicKey()
		{
			// create string version of _keys.pbl
			string PublicKey = "storage public key";

			string tokenAndPublicKey = $"{Token},{PublicKey}";

			tokenAndPublicKey = App.Encrypt(tokenAndPublicKey);
			string signature = App.Client.Sign(tokenAndPublicKey);

			string data = $"{signature},{tokenAndPublicKey}";
			return Obfuscator.Obfuscate(data);
		}

		public string Sign(string data)
		{
			return "signature";
		}

		public string Decrypt(string data)
		{
			return data;
		}

		public async Task StoreAsync(string thingId, string data)
		{
			data = Obfuscator.DeObfuscate(data);

			string[] signatureAndData = data.Split(new char[] { ',' }, 2);
			string signature = signatureAndData[0];
			data = signatureAndData[1];

			if (!App.Verify(data, signature))
				throw new Exception("Incorrect data.");

			data = Decrypt(data);

			string[] dataAndModifiedOn = data.Split(',');
			data = dataAndModifiedOn[0];

			// TODO: handle modifiedOn

			IDb db = await Manager.GetDbAsync();
			await db.StoreThingAsync(this, thingId, data);
		}

		public async Task<string> GetACopyAsync(string thingId)
		{
			IDb db = await Manager.GetDbAsync();

			string data = await db.GetThingCopyAsync(this, thingId);

			data = App.Encrypt(data);
			string signature = Sign(data);

			data = $"{signature},{data}";
			return Obfuscator.Obfuscate(data);
		}

		public async Task DiscardAsync(string thingId)
		{
			IDb db = await Manager.GetDbAsync();
			await db.DiscardThingAsync(this, thingId);
		}

	}
}
