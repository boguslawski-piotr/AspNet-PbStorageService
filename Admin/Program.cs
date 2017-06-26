using System;
using System.Net.Http;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Admin
{
	class Program
	{
		static Uri ServerUri = new Uri("http://localhost:50768/");
		static string ApiUri = "/api/storage/";

		static Uri StorageUri => new Uri(ServerUri, ApiUri);

		static async Task CreateNewClientAsync()
		{
			try
			{
				Console.WriteLine($"Creating new client in pbXStorage at {ServerUri}...");
				Console.WriteLine();

				string httpcmd = "GET";
				string cmd = "newclient";

				Uri uri = new Uri(StorageUri, cmd);

				string response = await pbXStorage.Client.Tools.ExecuteCommandAsync(httpcmd, uri);

				string[] clientData = response.Split(pbXStorage.Client.Tools.commaCharArray, 2);

				string clientId = clientData[0];
				RsaKeyPair clientPblKey = new RsaKeyPair(null, clientData[1]);

				Console.WriteLine($"ID:");
				Console.WriteLine($"{clientId}");
				Console.WriteLine($"Public Key:");
				Console.WriteLine($"{clientPblKey.Public}");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		static void Main(string[] args)
		{
			pbXStorage.Client.Tools.QuietMode = true;

			try
			{
				// Parse command line...

				bool nextShouldBeServerAddres = false;
				string command = null;

				foreach (var arg in args)
				{
					if (nextShouldBeServerAddres)
					{
						ServerUri = new Uri(arg);
						nextShouldBeServerAddres = false;
					}

					if (arg == "-s")
						nextShouldBeServerAddres = true;

					if (arg == "newclient")
						command = arg;
				}

				// Goto work...

				if (command != null)
				{
					switch (command)
					{
						case "newclient":
							CreateNewClientAsync().Wait();
							break;
					}

					//Console.ReadKey();
				}
				else
				{
					Console.WriteLine("pbXStorage Admin");
					Console.WriteLine("syntax: [admin executable] [options] command");

					Console.WriteLine();

					Console.WriteLine("Options:");
					Console.WriteLine("  -s server");

					Console.WriteLine();

					Console.WriteLine("Commands:");
					Console.WriteLine("  newclient Creates new client in pbXStorage");

				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}
}