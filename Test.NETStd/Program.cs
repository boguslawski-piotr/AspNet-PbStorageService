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
		static HttpClient http = new HttpClient();
		static Uri baseUri = new Uri("http://localhost:50768/api/storage/");

		// generated in app, should be stored in a safe way
		static string appPblKey = "app public key";
		static string appPrvKey = "app private key";

		// given from pbXStorage registration tool/web site
		static string clientId;
		static string clientPblKey;

		// given from server during communication
		static string appToken;

		static string storageToken;
		static string storagePblKey;

		static async Task<string> GetResponseContent(string cmd, Task<HttpResponseMessage> action)
		{
			try
			{
				HttpResponseMessage response = await action;
				if (response.IsSuccessStatusCode)
				{
					var content = await response.Content.ReadAsStringAsync();
					Console.WriteLine($"{cmd}: {content}");

					return content;
				}
				else
					throw new Exception(response.ToString());
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex.Message);
				if (ex.InnerException != null)
					Console.WriteLine(ex.InnerException.Message);
				throw ex;
			}
		}

		static async Task<string[]> NewClientTestAsync()
		{
			Uri uri = new Uri(baseUri, "newclient");
			string response = await GetResponseContent("newclient", http.GetAsync(uri));
			response = Obfuscator.DeObfuscate(response);

			string[] clientData = response.Split(',');

			clientId = clientData[0];
			clientId = "4c60bdb9d6d249ed92632ecf8d0f2267";
			clientPblKey = clientData[1];

			Console.WriteLine($"Client: {clientId} with public key: {clientPblKey}");
			Console.WriteLine();

			return clientData;
		}

		static async Task<string[]> RegisterAppTestAsync(string clientId)
		{
			Uri uri = new Uri(baseUri, $"registerapp/{clientId}");

			// encrypt appPblKey with clientPblKey
			var postData = new StringContent($"'{appPblKey}'", Encoding.UTF8, "application/json");

			string response = await GetResponseContent("registerapp", http.PostAsync(uri, postData));
			response = Obfuscator.DeObfuscate(response);

			string[] appData = response.Split(',');

			string signature = appData[0];
			appToken = appData[1];
			// verify appToken with clientPblKey

			Console.WriteLine($"Registered app: {appToken}, signature: {signature}");
			Console.WriteLine();

			return appData;
		}

		static async Task<string[]> OpenStorageTestAsync(string appToken, string storageId)
		{
			Uri uri = new Uri(baseUri, $"open/{appToken},{storageId}");

			string response = await GetResponseContent("open", http.GetAsync(uri));
			response = Obfuscator.DeObfuscate(response);

			string[] storageData = response.Split(new char[] { ',' }, 2);

			string signature = storageData[0];
			string data = storageData[1];

			// data => verify with clientPblKey, decrypt with appPrvKey

			string[] storageTokenAndPublicKey = data.Split(',');
			storageToken = storageTokenAndPublicKey[0];
			storagePblKey = storageTokenAndPublicKey[1];

			Console.WriteLine($"Opened storage: {storageToken} with public key: {storagePblKey}");
			Console.WriteLine();

			return storageData;
		}

		static async Task<string> StoreThingTestAsync(string storageToken, string thingId, string data)
		{
			Uri uri = new Uri(baseUri, $"store/{storageToken},{thingId}");

			data = $"{data},modifiedOn";

			// encrypt data with storagePblKey

			// sign data with appPrvKey

			data = $"signature,{data}";

			data = Obfuscator.Obfuscate(data);
			var postData = new StringContent($"'{data}'", Encoding.UTF8, "application/json");

			string response = await GetResponseContent("store", http.PutAsync(uri, postData));

			Console.WriteLine();

			return response;
		}

		static async Task<string[]> GetThingTestAsync(string storageToken, string thingId)
		{
			Uri uri = new Uri(baseUri, $"getacopy/{storageToken},{thingId}");

			string response = await GetResponseContent("getacopy", http.GetAsync(uri));
			response = Obfuscator.DeObfuscate(response);

			string[] signatureAndData = response.Split(',');
			string signature = signatureAndData[0];
			string thingData = signatureAndData[1];

			// thingData => verify with storagePblKey

			// thingData => decrypt with appPrvKey

			Console.WriteLine($"Thing: {thingData}");
			Console.WriteLine();

			return signatureAndData;
		}

		static async Task<string> DiscardThingTestAsync(string storageToken, string thingId)
		{
			Uri uri = new Uri(baseUri, $"discard/{storageToken},{thingId}");

			string response = await GetResponseContent("discard", http.DeleteAsync(uri));

			Console.WriteLine();

			return response;
		}

		static async Task TestsAsync()
		{
			await NewClientTestAsync();

			await RegisterAppTestAsync(clientId);

			await OpenStorageTestAsync(appToken, "test");

			await StoreThingTestAsync(storageToken, "test thing", "ala ma kota i psa ąęłóżść");

			await GetThingTestAsync(storageToken, "test thing");
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

		static void Main(string[] args)
		{
			http.Timeout = TimeSpan.FromSeconds(30);

			TestsAsync();

			//StressTestsAsync();

			Console.ReadKey();
		}
	}
}