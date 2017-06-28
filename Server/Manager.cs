using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using pbXNet;

// register (special util, web page, etc.) -> clientId, clientPublicKey
// app has:
//   clientId
//   clientPublicKey
// server has:
//   clientId
//   clientPublicKey
//   clientPrivateKey

// app run
// generate:
//   appPrivateKey
//   appPublicKey

// registerapp (post clientId) <- (appPublicKey) encrypted with clientPublicKey -> (appToken)
// app has:
//   appToken
// server has
//   appPublicKey

// open (get appToken, storageId) <- nothing -> (storageToken, storagePublicKey) encrypted with appPublicKey, and signed with clientPrivateKey
// app has:
//   storageToken
//   storagePublicKey
// server has
//   storageToken
//   storagePrivateKey

// store (put storageToken, thingId) <- (data) encrypted with storagePublicKey, and signed with appPrivateKey -> OK or error

// getACopy (get storageToken, thingId) <- nothing -> (modifiedOn,data) encrypted with appPublicKey, and signed with storagePrivateKey

// findIds (get storageToken, pattern) <- nothing -> (ids list separated with |) encrypted with appPublicKey, and signed with storagePrivateKey

namespace pbXStorage.Server
{
	public class Manager
	{
		string _id = "f406bd73571d4e11a0221d457b8589cc";
		public string Id {
			get => _id;
			set {
				if (value != null)
				{
					_id = value;
					Log.I($"Id is set to: {_id}'.", this);
				}
			}
		} 

		public ISerializer Serializer { get; set; }

		public ISimpleCryptographer Cryptographer { get; set; }

		public IDb Db { get; set; }

		ConcurrentDictionary<string, Client> _clients = new ConcurrentDictionary<string, Client>();

		ConcurrentDictionary<string, App> _apps = new ConcurrentDictionary<string, App>();

		ConcurrentDictionary<string, Storage> _storages = new ConcurrentDictionary<string, Storage>();

		public async Task InitializeAsync()
		{
			if (Serializer == null || Db == null)
				throw new ArgumentException($"{nameof(Serializer)} and {nameof(Db)} must be valid objects.");

			if (!Db.Initialized)
				await Db.InitializeAsync();

			await LoadClientsAsync();
		}

		#region Clients

		const string _clientsThingId = "c235e54b577c44418ab0-948f299e8308";

		readonly SemaphoreSlim _clientsLock = new SemaphoreSlim(1);

		public async Task LoadClientsAsync()
		{
			await _clientsLock.WaitAsync().ConfigureAwait(false);
			try
			{
				if (await Db.ThingExistsAsync(Id, _clientsThingId))
				{
					string d = await Db.GetThingCopyAsync(Id, _clientsThingId).ConfigureAwait(false);
					if (d != null)
					{
						if (Cryptographer != null)
							d = Cryptographer.Decrypt(d);

						d = Obfuscator.DeObfuscate(d);

						_clients = Serializer.Deserialize<ConcurrentDictionary<string, Client>>(d);

						foreach (var client in _clients.Values)
							await client.InitializeAfterDeserializeAsync(this).ConfigureAwait(false);

					}
				}

				Log.I($"{_clients?.Values.Count} client(s) definition loaded.", this);
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
				throw ex;
			}
			finally
			{
				_clientsLock.Release();
			}
		}

		public async Task SaveClientsAsync()
		{
			await _clientsLock.WaitAsync().ConfigureAwait(false);
			try
			{
				string d = Serializer.Serialize(_clients);

				d = Obfuscator.Obfuscate(d);

				if (Cryptographer != null)
					d = Cryptographer.Encrypt(d);

				await Db.StoreThingAsync(Id, _clientsThingId, d).ConfigureAwait(false);
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

		public async Task<string> NewClientAsync()
		{
			try
			{
				Client client = Client.New(this);

				_clients[client.Id] = client;

				await SaveClientsAsync().ConfigureAwait(false);

				string rc = client.GetIdAndPublicKey();

				return OK(rc);
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
			}

			return Error("Failed to create client.");
		}

		#endregion

		#region Apps

		public async Task<string> RegisterAppAsync(string clientId, string appPublicKey)
		{
			if (_clients.TryGetValue(clientId, out Client client))
			{
				try
				{
					appPublicKey = GET(appPublicKey);

					App app = _apps.Values.ToList().Find((_app) => (_app.PublicKey == appPublicKey && _app.Client == client));
					if (app == null)
					{
						app = new App(this, client, appPublicKey);
						_apps[app.Token] = app;

						Log.I($"created new app '{app.Token}' for '{client.Id}'.", this);
					}

					if (app != null)
						return OK(app.Token);
				}
				catch (Exception ex)
				{
					return Error(ex.Message);
				}

				return Error("Failed to register application.");
			}

			return Error($"Client '{clientId}' doesn't exist.");
		}

		#endregion

		#region Storages

		public async Task<string> OpenStorageAsync(string appToken, string storageId)
		{
			if (_apps.TryGetValue(appToken, out App app))
			{
				try
				{
					Storage storage = _storages.Values.ToList().Find((_storage) => (_storage.App.Token == appToken && _storage.Id == storageId));
					if (storage == null)
					{
						storage = new Storage(this, app, storageId);
						_storages[storage.Token] = storage;

						Log.I($"created '{storageId}' for app '{app.Token}'.", this);
					}

					if (storage != null)
					{
						Log.I($"opened '{storageId}' for app '{app.Token}'.", this);

						return OK(storage.TokenAndPublicKey);
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

		#endregion

		#region Things

		async Task<string> ExecuteInStorage(string storageToken, Func<Storage, Task<string>> action, [CallerMemberName]string callerName = null)
		{
			if (_storages.TryGetValue(storageToken, out Storage storage))
			{
				try
				{
					Task<string> task = action(storage);
					return await task.ConfigureAwait(false);
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
				await storage.StoreAsync(thingId, GET(data)).ConfigureAwait(false);
				return OK();
			}).ConfigureAwait(false);
		}

		public async Task<string> ThingExistsAsync(string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.ExistsAsync(thingId).ConfigureAwait(false));
			}).ConfigureAwait(false);
		}

		public async Task<string> GetThingModifiedOnAsync(string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.GetModifiedOnAsync(thingId).ConfigureAwait(false));
			}).ConfigureAwait(false);
		}

		public async Task<string> GetThingCopyAsync(string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.GetACopyAsync(thingId).ConfigureAwait(false));
			}).ConfigureAwait(false);
		}

		public async Task<string> DiscardThingAsync(string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				await storage.DiscardAsync(thingId).ConfigureAwait(false);
				return OK();
			}).ConfigureAwait(false);
		}

		public async Task<string> FindThingIdsAsync(string storageToken, string pattern)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.FindIdsAsync(pattern).ConfigureAwait(false));
			}).ConfigureAwait(false);
		}

		#endregion

		#region Tools

		string GET(string data)
		{
			return Obfuscator.DeObfuscate(data);
		}

		string OK(string data = null, [CallerMemberName]string callerName = null)
		{
			data = "OK" + (data != null ? $",{data}" : "");
			Log.I(data, this, callerName);
			return Obfuscator.Obfuscate(data);
		}

		string Error(string message, [CallerMemberName]string callerName = null)
		{
			Log.E(message, this, callerName);
			return Obfuscator.Obfuscate($"ERROR,{message}");
		}

		#endregion
	}
}
