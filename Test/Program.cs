using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using pbXNet;

namespace Test.NETStd
{
	class Program
	{
		static Uri ApiUri = new Uri("http://10.211.55.3:23456/api/storage/");

		// generated in app
		static IAsymmetricCryptographerKeyPair appKeys;

		// given from pbXStorage admin tool/web site
		static string clientId;
		static IAsymmetricCryptographerKeyPair clientPblKey;

		// given from server during communication
		static string appToken;
		static string storageToken;
		static IAsymmetricCryptographerKeyPair storagePblKey;

		static RsaCryptographer cryptographer;

		static async Task InitializeAsync()
		{
			cryptographer = new RsaCryptographer();
			appKeys = cryptographer.GenerateKeyPair();
		}

		static async Task NewClientTestAsync()
		{
			//string httpcmd = "GET";
			//string cmd = "newclient";

			//Uri uri = new Uri(ApiUri, cmd);

			//string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);

			//string[] clientData = response.Split(pbXStorage.Client.Tools.commaCharArray, 2);
			//clientId = clientData[0];
			//clientPblKey = new RsaKeyPair(null, clientData[1]);

			clientId = "1bc3f461f9ad4a40ac4f6a366c209484636342939872347169";
			clientPblKey = new RsaKeyPair(null, "FZBJEkQxCEJP9Ks0JkaXxOH+R2p7xYYHFN/3ERMRf9+LspdJDoTc8gxvexbWUXxXvY7VRIq8eBu1yz0brMlPRbdruuTJwFtHY/EN3Wy3h0eAyMuKeMLEfCcJnykpVlkcxig5JiA1O3c9ZSPL0wcMNk1c5Um5bQGs2jMtCAWbTulyvptrGB1/zDSxjPojco8HTP3sXI5x9VM9UUuQNurRXL7mgR8=");

			Console.WriteLine();
			Console.WriteLine($"Client: {clientId} with public key: {clientPblKey.Public}");
			Console.WriteLine();
		}

		static async Task RegisterAppTestAsync(string clientId)
		{
			string httpcmd = "POST";
			string cmd = "registerapp";

			Uri uri = new Uri(ApiUri, $"{cmd}/{clientId}");

			string data = appKeys.Public;

			data = RsaCryptographerHelper.Encrypt(data, clientPblKey);

			data = Obfuscator.Obfuscate(data);

			var postData = new StringContent($"'{data}'", Encoding.UTF8, "application/json");

			string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri, postData);

			appToken = response;

			Console.WriteLine();
			Console.WriteLine($"Registered app: {appToken}");
			Console.WriteLine();
		}

		static async Task OpenStorageTestAsync(string appToken, string storageId)
		{
			string httpcmd = "GET";
			string cmd = "open";

			Uri uri = new Uri(ApiUri, $"{cmd}/{appToken},{storageId}");

			string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);

			string[] storageData = response.Split(pbXStorage.Client.Tools.commaCharArray, 2);

			string signature = storageData[0];
			string data = storageData[1];

			bool ok = RsaCryptographerHelper.Verify(data, signature, clientPblKey);
			if (!ok)
			{
				Console.WriteLine("ERROR: data NOT verified.");
				return;
			}

			data = RsaCryptographerHelper.Decrypt(data, appKeys);

			string[] storageTokenAndPublicKey = data.Split(pbXStorage.Client.Tools.commaCharArray, 2);
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

			Uri uri = new Uri(ApiUri, $"{cmd}/{storageToken},{thingId}");

			long bModifiedOn = modifiedOn.ToUniversalTime().ToBinary();

			data = $"{bModifiedOn.ToString()},{data}";

			data = RsaCryptographerHelper.Encrypt(data, storagePblKey);

			string signature = RsaCryptographerHelper.Sign(data, appKeys);

			data = $"{signature},{data}";

			data = Obfuscator.Obfuscate(data);

			var postData = new StringContent($"'{data}'", Encoding.UTF8, "application/json");

			await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri, postData);

			Console.WriteLine();
		}

		static async Task ThingExistsTestAsync(string storageToken, string thingId)
		{
			string httpcmd = "GET";
			string cmd = "exists";

			Uri uri = new Uri(ApiUri, $"{cmd}/{storageToken},{thingId}");

			string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);
			// response == YES or NO

			Console.WriteLine();
		}

		static async Task GetThingModifiedOnTestAsync(string storageToken, string thingId)
		{
			string httpcmd = "GET";
			string cmd = "getmodifiedon";

			Uri uri = new Uri(ApiUri, $"{cmd}/{storageToken},{thingId}");

			string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);
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

			Uri uri = new Uri(ApiUri, $"{cmd}/{storageToken},{thingId}");

			string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);

			string[] signatureAndData = response.Split(pbXStorage.Client.Tools.commaCharArray, 2);
			string signature = signatureAndData[0];
			string thingData = signatureAndData[1];

			bool ok = RsaCryptographerHelper.Verify(thingData, signature, storagePblKey);
			if (!ok)
			{
				Console.WriteLine("ERROR: data NOT verified.");
				return;
			}

			thingData = RsaCryptographerHelper.Decrypt(thingData, appKeys);
			// modifiedOn,data

			Console.WriteLine();
			Console.WriteLine($"Thing all data: {thingData}");

			string[] modifiedOnAndData = thingData.Split(pbXStorage.Client.Tools.commaCharArray, 2);
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

			Uri uri = new Uri(ApiUri, $"{cmd}/{storageToken},{thingId}");

			await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);

			Console.WriteLine();
		}

		static async Task FindThingIdsTestAsync(string storageToken, string pattern)
		{
			string httpcmd = "GET";
			string cmd = "findids";

			if (string.IsNullOrWhiteSpace(pattern))
				pattern = " ";

			pattern = Uri.EscapeDataString(pattern);

			Uri uri = new Uri(ApiUri, $"{cmd}/{storageToken},{pattern}");

			string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);
			// response == null or encrypted/signed id list with separator |

			Console.WriteLine();

			if (response != null)
			{
				string[] signatureAndIds = response.Split(pbXStorage.Client.Tools.commaCharArray, 2);
				string signature = signatureAndIds[0];
				string ids = signatureAndIds[1];

				bool ok = RsaCryptographerHelper.Verify(ids, signature, storagePblKey);
				if (!ok)
				{
					Console.WriteLine("ERROR: data NOT verified.");
					return;
				}

				ids = RsaCryptographerHelper.Decrypt(ids, appKeys);

				Console.WriteLine($"Found: {ids}");
			}
			else
				Console.WriteLine($"Found: nothing");

			Console.WriteLine();
		}

		static async Task TestsAsync()
		{
			await NewClientTestAsync();

			await RegisterAppTestAsync(clientId);

			await OpenStorageTestAsync(appToken, "test");

			await OpenStorageTestAsync(appToken, "test2");

			await OpenStorageTestAsync(appToken, "test");

			await StoreThingTestAsync(storageToken, "test thing", "ala ma kota i psa ąęłóżść", (DateTime.Now - TimeSpan.FromHours(3)));

			//for (int i = 0; i < 100; i++)
			//{
			//	await StoreThingTestAsync(storageToken, "test thing " + i.ToString(), "ala ma kota i psa ąęłóżść", (DateTime.Now - TimeSpan.FromHours(3)));
			//}

			await ThingExistsTestAsync(storageToken, "test thing");

			await ThingExistsTestAsync(storageToken, "test thing W");

			await GetThingModifiedOnTestAsync(storageToken, "test thing");

			await GetThingTestAsync(storageToken, "test thing");

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
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		static void Main(string[] args)
		{
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