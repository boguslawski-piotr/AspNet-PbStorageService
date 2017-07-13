using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.TestClient
{
	class Program
	{
		static PbXStorageSettings pbXStorageSettings = new PbXStorageSettings()
		{
			ApiUri = new Uri("http://10.211.55.3:23456/api/storage/"),
			AppKeys = new RsaCryptographer().GenerateKeyPair(),
			RepositoryId = "b455f321bc1341f0b20a21f2d305ec12636354891142899900",
			RepositoryPublicKey = new RsaKeyPair(null, "DU9JDgAxCHrRJG5oPVpr//+kqQdCIAH8vo+YiPj72kTJRi/cDoqw5IIdQe2yYtsGB2C8ObocFQ3gxhOSp3QTgc9aktUdh0eTxHYPPXoOEzydTB8o9VLLdUQTozK3ZAjoYm+vkVYPXjvWiwjlW4dLzJUl8tBkxpSA9L5p5OUJk8tqVbapWnR19Ytx5AnUys49z1+t/X6qA657FRbz7tV44Pt+"),
		};

		// given from server during communication
		static string appToken;
		static string storageToken;
		static IAsymmetricCryptographerKeyPair storagePblKey;

		public static HttpClient httpClient;

		public static async Task<string> ExecuteCommandAsync(string cmd, Uri uri, HttpContent content = null)
		{
			if (httpClient == null)
			{
				httpClient = new HttpClient();
				httpClient.Timeout = TimeSpan.FromSeconds(30);
			}

			return await StorageOnPbXStorage.ExecuteCommandAsync(httpClient, cmd, uri, content);
		}

		static async Task InitializeAsync()
		{
			Console.WriteLine($"Repository: {pbXStorageSettings.RepositoryId}");
			Console.WriteLine();
		}

		static async Task RegisterAppTestAsync(string repositoryId)
		{
			string httpcmd = "POST";
			string cmd = "registerapp";

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{repositoryId}");

			string data = pbXStorageSettings.AppKeys.Public;

			data = RsaCryptographerHelper.Encrypt(data, pbXStorageSettings.RepositoryPublicKey);

			data = Obfuscator.Obfuscate(data);

			var postData = new StringContent($"'{data}'", Encoding.UTF8, "application/json");

			string response = await ExecuteCommandAsync(httpcmd, uri, postData);

			appToken = response;

			Console.WriteLine();
			Console.WriteLine($"Registered app: {appToken}");
			Console.WriteLine();
		}

		static async Task OpenStorageTestAsync(string appToken, string storageId)
		{
			string httpcmd = "GET";
			string cmd = "open";

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{appToken},{storageId}");

			string response = await ExecuteCommandAsync(httpcmd, uri);

			string[] storageData = response.Split(StorageOnPbXStorage.commaAsArray, 2);

			string signature = storageData[0];
			string data = storageData[1];

			bool ok = RsaCryptographerHelper.Verify(data, signature, pbXStorageSettings.RepositoryPublicKey);
			if (!ok)
			{
				Console.WriteLine("ERROR: data NOT verified.");
				return;
			}

			data = RsaCryptographerHelper.Decrypt(data, pbXStorageSettings.AppKeys);

			string[] storageTokenAndPublicKey = data.Split(StorageOnPbXStorage.commaAsArray, 2);
			storageToken = storageTokenAndPublicKey[0];
			storagePblKey = new RsaKeyPair(null, storageTokenAndPublicKey[1]);

			Console.WriteLine();
			Console.WriteLine($"Opened storage: {storageToken} with public key: {storagePblKey.Public}");
			Console.WriteLine();
		}

		static async Task StoreThingTestAsync(string storageToken, string thingId, string data, DateTime modifiedOn)
		{
			string httpcmd = "PUT";
			string cmd = "store";

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{storageToken},{thingId}");

			long bModifiedOn = modifiedOn.ToUniversalTime().ToBinary();

			data = $"{bModifiedOn.ToString()},{data}";

			data = RsaCryptographerHelper.Encrypt(data, storagePblKey);

			string signature = RsaCryptographerHelper.Sign(data, pbXStorageSettings.AppKeys);

			data = $"{signature},{data}";

			data = Obfuscator.Obfuscate(data);

			var postData = new StringContent($"'{data}'", Encoding.UTF8, "application/json");

			await ExecuteCommandAsync(httpcmd, uri, postData);

			Console.WriteLine();
		}

		static async Task ThingExistsTestAsync(string storageToken, string thingId)
		{
			string httpcmd = "GET";
			string cmd = "exists";

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{storageToken},{thingId}");

			string response = await ExecuteCommandAsync(httpcmd, uri);
			// response == YES or NO

			Console.WriteLine();
		}

		static async Task GetThingModifiedOnTestAsync(string storageToken, string thingId)
		{
			string httpcmd = "GET";
			string cmd = "getmodifiedon";

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{storageToken},{thingId}");

			string response = await ExecuteCommandAsync(httpcmd, uri);
			// response == DateTime as binary

			DateTime modifiedOn = DateTime.FromBinary(long.Parse(response));
			DateTime localModifiedOn = modifiedOn.ToLocalTime();

			Console.WriteLine();
			Console.WriteLine($"Thing modified on: UTC: {modifiedOn}, Local: {localModifiedOn}");
			Console.WriteLine();
		}

		static async Task GetThingTestAsync(string storageToken, string thingId)
		{
			string httpcmd = "GET";
			string cmd = "getacopy";

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{storageToken},{thingId}");

			string response = await ExecuteCommandAsync(httpcmd, uri);

			string[] signatureAndData = response.Split(StorageOnPbXStorage.commaAsArray, 2);
			string signature = signatureAndData[0];
			string thingData = signatureAndData[1];

			bool ok = RsaCryptographerHelper.Verify(thingData, signature, storagePblKey);
			if (!ok)
			{
				Console.WriteLine("ERROR: data NOT verified.");
				return;
			}

			thingData = RsaCryptographerHelper.Decrypt(thingData, pbXStorageSettings.AppKeys);
			// modifiedOn,data

			Console.WriteLine();
			Console.WriteLine($"Thing all data: {thingData}");

			string[] modifiedOnAndData = thingData.Split(StorageOnPbXStorage.commaAsArray, 2);
			DateTime modifiedOn = DateTime.FromBinary(long.Parse(modifiedOnAndData[0]));
			DateTime localModifiedOn = modifiedOn.ToLocalTime();
			thingData = modifiedOnAndData[1];

			Console.WriteLine($"Thing data: {thingData}");
			Console.WriteLine($"Thing modified on: UTC: {modifiedOn}, Local: {localModifiedOn}");
			Console.WriteLine();
		}

		static async Task DiscardThingTestAsync(string storageToken, string thingId)
		{
			string httpcmd = "DELETE";
			string cmd = "discard";

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{storageToken},{thingId}");

			await ExecuteCommandAsync(httpcmd, uri);

			Console.WriteLine();
		}

		static async Task FindThingIdsTestAsync(string storageToken, string pattern)
		{
			string httpcmd = "GET";
			string cmd = "findids";

			if (string.IsNullOrWhiteSpace(pattern))
				pattern = " ";

			pattern = Uri.EscapeDataString(pattern);

			Uri uri = new Uri(pbXStorageSettings.ApiUri, $"{cmd}/{storageToken},{pattern}");

			string response = await ExecuteCommandAsync(httpcmd, uri);
			// response == null or encrypted/signed id list with separator |

			Console.WriteLine();

			if (response != null)
			{
				string[] signatureAndIds = response.Split(StorageOnPbXStorage.commaAsArray, 2);
				string signature = signatureAndIds[0];
				string ids = signatureAndIds[1];

				bool ok = RsaCryptographerHelper.Verify(ids, signature, storagePblKey);
				if (!ok)
				{
					Console.WriteLine("ERROR: data NOT verified.");
					return;
				}

				ids = RsaCryptographerHelper.Decrypt(ids, pbXStorageSettings.AppKeys);

				Console.WriteLine($"Found: {ids}");
			}
			else
				Console.WriteLine($"Found: nothing");

			Console.WriteLine();
		}

		static async Task TestsAsync()
		{
			await RegisterAppTestAsync(pbXStorageSettings.RepositoryId);

			await OpenStorageTestAsync(appToken, "test");

			await OpenStorageTestAsync(appToken, "test2");

			//List<Task> l = new List<Task>();
			for (int i = 0; i < 100; i++)
			{
				//l.Add(StoreThingTestAsync(storageToken, "test thing " + i.ToString(), "jakies dane....", (DateTime.Now - TimeSpan.FromHours(3))));
				await StoreThingTestAsync(storageToken, "test thing " + i.ToString(), "jakies dane....", (DateTime.Now - TimeSpan.FromHours(3)));
			}
			//await Task.WhenAll(l);

			await OpenStorageTestAsync(appToken, "test");

			await StoreThingTestAsync(storageToken, "test thing", "ala ma kota i psa ąęłóżść", (DateTime.Now - TimeSpan.FromHours(3)));

			for (int i = 0; i < 15; i++)
			{
				await StoreThingTestAsync(storageToken, "test thing " + i.ToString(), "ala ma kota i psa ąęłóżść", (DateTime.Now - TimeSpan.FromHours(3)));
			}

			await ThingExistsTestAsync(storageToken, "test thing");

			await ThingExistsTestAsync(storageToken, "test thing W");

			await GetThingModifiedOnTestAsync(storageToken, "test thing");

			try
			{
				await GetThingModifiedOnTestAsync(storageToken, "test thing W");
			}
			catch (StorageThingNotFoundException) { }

			await GetThingTestAsync(storageToken, "test thing");

			try
			{
				await GetThingTestAsync(storageToken, "test thing W");
			}
			catch (StorageThingNotFoundException) { }

			//await DiscardThingTestAsync(storageToken, "test thing");

			await FindThingIdsTestAsync(storageToken, "");

			await FindThingIdsTestAsync(storageToken, "9$");

			await FindThingIdsTestAsync(storageToken, "a9$");
		}

		static async Task StressTestsAsync()
		{
			List<Task> l = new List<Task>();
			for (int i = 0; i < 100; i++)
			{
				l.Add(TestsAsync());
			}

			await Task.WhenAll(l);
		}

		static async Task StartTestsAsync()
		{
			try
			{
				await InitializeAsync();

				await TestsAsync();

				//await StressTestsAsync();
			}
			catch (StorageOnPbXStorageException ex)
			{
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		static void Main(string[] args)
		{
			Log.AddLogger(new ConsoleLogger());

			StartTestsAsync();

			// RSA

			//RsaCryptographer c = new RsaCryptographer();
			//IAsymmetricCryptographerKeyPair keys = c.GenerateKeyPair();

			//RsaKeyPair rkeysprv = new RsaKeyPair(keys.Private, null);
			//RsaKeyPair rkeyspbl = new RsaKeyPair(null, keys.Public);

			//ByteBuffer b = c.Encrypt(new ByteBuffer("01234567890123456789012345678901234567890123456789012345678912345!", Encoding.UTF8), rkeyspbl);
			//Console.WriteLine(b.ToHexString());

			//ByteBuffer s = c.Sign(b, rkeysprv);
			//Console.WriteLine(s.ToHexString());

			//bool ok = c.Verify(b, s, rkeyspbl);
			//Console.WriteLine($"{ok}");

			//string d = c.Decrypt(b, rkeysprv).ToString(Encoding.UTF8);
			//Console.WriteLine(d);

			Console.ReadKey();
		}
	}
}