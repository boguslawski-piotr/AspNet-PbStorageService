using System;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class Storage
	{
		public App App { get; }

		public string Id { get; }
		public string Token { get; }

		string PublicKey;
		string PrivateKey;

		public Storage(App app, string id)
		{
			App = app ?? throw new ArgumentNullException(nameof(app));
			Id = id ?? throw new ArgumentNullException(nameof(id));

			Token = Tools.CreateGuid();

			// TODO: create key pair
		}

		public string GetTokenAndPublicKey()
		{
			string tokenAndPublicKey = $"{Token},{PublicKey}";

			//tokenAndPublicKey = App.Encrypt(tokenAndPublicKey);
			//string signature = App.Client.Sign(tokenAndPublicKey);
			string signature = "signature";

			return $"{signature},{tokenAndPublicKey}";
		}

		public async Task<bool> StoreAsync(string thingId, string data)
		{
			// split data into signature and data

			// verify signature with App.PublicKey

			// decrypt data with PrivateKey

			// store data to fs

			IFileSystem fs = await FileSystem.GetAsync(this);

			await fs.WriteTextAsync(thingId, data);

			return true;
		}

		public async Task<string> GetACopyAsync(string thingId)
		{
			// get data from fs

			IFileSystem fs = await FileSystem.GetAsync(this);

			// encrypt with App.PublicKey

			// sign with privateKey

			return "signature,data";
		}
	}
}
