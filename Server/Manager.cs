using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using pbXNet;
using System.Linq;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

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
	public class Base
	{
		protected Manager Manager;

		public Base(Manager manager)
		{
			Manager = manager;
		}
	}

	public class Manager
	{
		Lazy<ISerializer> _serializer = new Lazy<ISerializer>(() => new NewtonsoftJsonSerializer(), true);
		ISerializer Serializer => _serializer.Value;

		ConcurrentDictionary<string, Client> _clients = new ConcurrentDictionary<string, Client>();

		ConcurrentDictionary<string, App> _apps = new ConcurrentDictionary<string, App>();

		ConcurrentDictionary<string, Storage> _storages = new ConcurrentDictionary<string, Storage>();

		public async Task InitializeAsync()
		{
			await LoadClientsAsync();
		}

		public async Task<string> NewClientAsync()
		{
			try
			{
				Client client = Client.New(this);

				_clients[client.Id] = client;

				await SaveClientsAsync();

				string rc = client.GetIdAndPublicKey();

				Log.I(rc);

				return rc;
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
			}

			return Error("Failed to create client.");
		}

		public async Task<string> RegisterAppAsync(string clientId, string appPublicKey)
		{
			if (_clients.TryGetValue(clientId, out Client client))
			{
				try
				{
					App app = _apps.Values.ToList().Find((_app) => (_app.PublicKey == appPublicKey && _app.Client == client));
					if (app == null)
						app = new App(this, client, appPublicKey);

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

				return Error("Failed to register application.");
			}

			return Error($"Client '{clientId}' doesn't exist.");
		}

		public async Task<string> OpenStorageAsync(string appToken, string storageId)
		{
			if (_apps.TryGetValue(appToken, out App app))
			{
				try
				{
					Storage storage = _storages.Values.ToList().Find((_storage) => _storage.App.Token == appToken);
					if (storage == null)
						storage = new Storage(this, app, storageId);

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

				return Error("Failed to create/open storage.");
			}

			return Error($"Incorrect application token '{appToken}'.");
		}

		async Task<string> ExecuteInStorage(string storageToken, Func<Storage, Task<string>> action, [CallerMemberName]string callerName = null)
		{
			if (_storages.TryGetValue(storageToken, out Storage storage))
			{
				try
				{
					Task<string> task = action(storage);
					return await task;
				}
				catch (Exception ex)
				{
					return Error(ex.Message, callerName);
				}
			}

			return Error($"Incorrect storage token '{storageToken}'.", callerName);
		}

		public async Task<string> StoreThingAsync(string storageToken, string thingId, string data)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				await storage.StoreAsync(thingId, data);
				return OK();
			});
		}

		public async Task<string> GetThingCopyAsync(string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return await storage.GetACopyAsync(thingId);
			});
		}

		public async Task<string> DiscardThingAsync(string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				await storage.DiscardAsync(thingId);
				return OK();
			});
		}

		//
		// Tools
		//

		IDb _db;

		public async Task<IDb> GetDbAsync()
		{
			if (_db == null)
			{
				_db = new DbOnFileSystem();
				await _db.InitializeAsync();
			}

			return _db;
		}

		readonly SemaphoreSlim _clientsLock = new SemaphoreSlim(1);

		public async Task LoadClientsAsync()
		{
			await _clientsLock.WaitAsync();
			try
			{
				IDb db = await GetDbAsync();

				string d = await db.GetClientsAsync();
				if (d != null)
				{
					// TODO: decrypt and deobfuscate data (d)

					_clients = Serializer.Deserialize<ConcurrentDictionary<string, Client>>(d);

					foreach (var client in _clients.Values)
						await client.InitializeAfterDeserializeAsync(this);
				}
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
			}
			finally
			{
				_clientsLock.Release();
			}
		}

		public async Task SaveClientsAsync()
		{
			await _clientsLock.WaitAsync();
			try
			{
				string d = Serializer.Serialize(_clients);

				// TODO: obfuscate and encrypt data (d)

				IDb db = await GetDbAsync();
				await db.StoreClientsAsync(d);
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
			}
			finally
			{
				_clientsLock.Release();
			}
		}

		string OK()
		{
			return "OK";
		}

		string Error(string message, [CallerMemberName]string callerName = null)
		{
			Log.E(message, this, callerName);
			return $"ERROR,{message}";
		}
	}
}
