using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using pbXNet;
using System.Linq;

namespace pbXStorage.App
{
	public class App
	{
		// generated in App
		public string appPublicKey;
		public string appPrivateKey;

		// from pbXStorage tool/website
		public string clientId;
		public string clientPublicKey;

		// from communication
		public string appToken;
		public string storageToken;
		public string storagePublicKey;
	}
}

namespace pbXStorage.Server
{
	public class Manager
	{
		const string _clientsFileName = ".clients";

		IDictionary<string, Client> _clients = new Dictionary<string, Client>();

		IDictionary<string, App> _apps = new Dictionary<string, App>();

		IDictionary<string, Storage> _storages = new Dictionary<string, Storage>();

		public async Task<string> NewClientAsync()
		{
			Client client = Client.New();

			_clients[client.Id] = client;

			await SaveClientsAsync();

			return client.GetIdAndPublicKey();
		}

		public async Task<string> RegisterAppAsync(string clientId, string appPublicKey)
		{
			if (_clients.TryGetValue(clientId, out Client client))
			{
				try
				{
					App app = _apps.Values.ToList().Find((_app) => (_app.PublicKey == appPublicKey && _app.Client == client));
					if (app == null)
						app = new App(client, appPublicKey);

					if (app != null)
					{
						_apps[app.Token] = app;
						return app.GetToken();
					}
				}
				catch (Exception ex)
				{
					return Error(ex.Message);
				}

				return Error("Failed to register application");
			}

			return Error($"Client {clientId} doesn't exist");
		}

		public async Task<string> OpenStorageAsync(string appToken, string storageId)
		{
			if (_apps.TryGetValue(appToken, out App app))
			{
				try
				{
					Storage storage = _storages.Values.ToList().Find((_storage) => _storage.App.Token == appToken);
					if (storage == null)
						storage = new Storage(app, storageId);

					if (storage != null)
					{
						_storages[storage.Token] = storage;
						return storage.GetTokenAndPublicKey();
					}
				}
				catch (Exception ex)
				{
					return Error(ex.Message);
				}

				return Error("Failed to create/open storage");
			}

			return Error($"Incorrect application token {appToken}");
		}

		public async Task<string> StoreAsync(string storageToken, string thingId, string data)
		{
			if (_storages.TryGetValue(storageToken, out Storage storage))
			{
				try
				{
					if (await storage.StoreAsync(thingId, data))
						return OK();
				}
				catch (Exception ex)
				{
					return Error(ex.Message);
				}

				return Error("Failed to store data");
			}

			return Error($"Incorrect storage token {storageToken}");
		}

		public async Task<string> GetACopyAsync(string storageToken, string thingId)
		{
			if (_storages.TryGetValue(storageToken, out Storage storage))
			{
				try
				{
					return await storage.GetACopyAsync(thingId);
				}
				catch (Exception ex)
				{
					return Error(ex.Message);
				}
			}

			return Error($"Incorrect storage token {storageToken}");
		}


		//
		// Tools
		//

		public async Task LoadClientsAsync()
		{
			IFileSystem fs = await FileSystem.GetAsync();

			if (await fs.FileExistsAsync(_clientsFileName))
			{
				string d = await fs.ReadTextAsync(_clientsFileName);

				// TODO: decrypt and deobfuscate data (d)

				_clients = Serializer.Get().Deserialize<IDictionary<string, Client>>(d);

				foreach (var client in _clients.Values)
					client.InitializeAfterDeserialize();
			}
		}

		public async Task SaveClientsAsync()
		{
			IFileSystem fs = await FileSystem.GetAsync();

			string d = Serializer.Get().Serialize(_clients);

			// TODO: obfuscate and encrypt data (d)

			await fs.WriteTextAsync(_clientsFileName, d);
		}

		public static string OK()
		{
			return "OK";
		}

		public static string Error(string message)
		{
			return $"ERROR,{message}";
		}
	}

	public static class Serializer
	{
		static ISerializer _serializer;

		public static ISerializer Get()
		{
			if (_serializer == null)
				_serializer = new NewtonsoftJsonSerializer();
			return _serializer;
		}
	}

	public static class FileSystem
	{
		public static async Task<IFileSystem> GetAsync()
		{
			string homePath = Environment.GetEnvironmentVariable("HOME");
			if (homePath == null)
				homePath = Environment.GetEnvironmentVariable("HOMEPATH");

			DeviceFileSystem fs = new DeviceFileSystem(DeviceFileSystemRoot.UserDefined, homePath);

			await fs.CreateDirectoryAsync(".pbXStorage");

			return fs;
		}

		public static async Task<IFileSystem> GetAsync(Storage storage)
		{
			IFileSystem fs = await GetAsync();

			await fs.CreateDirectoryAsync(storage.App.Client.Id);

			await fs.CreateDirectoryAsync(storage.Id);

			return fs;
		}
	}
}
