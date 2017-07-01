using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using pbXNet;

// register (web page, etc.) -> repositoryId, repositoryPublicKey
// app has:
//   repositoryId
//   repositoryPublicKey
// server has:
//   repositoryId
//   repositoryPublicKey
//   repositoryPrivateKey

// app run & generate:
//   appPrivateKey
//   appPublicKey

// registerapp (post repositoryId) <- (appPublicKey) encrypted with repositoryPublicKey -> (appToken)
// app has:
//   appToken
// server has
//   appPublicKey

// open (get appToken, storageId) <- nothing -> (storageToken, storagePublicKey) encrypted with appPublicKey, and signed with repositoryPrivateKey
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
		public string Id { get; set; }

		public IDb Db { get; set; }

		public ISimpleCryptographer Cryptographer { get; set; }

		ConcurrentDictionary<string, Repository> _repositories = new ConcurrentDictionary<string, Repository>();

		ConcurrentDictionary<string, App> _apps = new ConcurrentDictionary<string, App>();

		ConcurrentDictionary<string, Storage> _storages = new ConcurrentDictionary<string, Storage>();

		ISerializer _serializer { get; set; } = new NewtonsoftJsonSerializer();

		public async Task InitializeAsync()
		{
			if (Id == null || Db == null)
				throw new ArgumentException($"{nameof(Id)} and {nameof(Db)} must be valid objects.");

			if (!Db.Initialized)
				await Db.InitializeAsync();

			await LoadRepositoriesAsync();
		}

		#region Repositories

		const string _repositoriesThingId = "c235e54b577c44418ab0948f299e830876b14f2cc39742f3bc4dd5ae581d1e31";

		readonly SemaphoreSlim _repositoriesLock = new SemaphoreSlim(1);

		public async Task LoadRepositoriesAsync()
		{
			await _repositoriesLock.WaitAsync().ConfigureAwait(false);
			try
			{
				if (await Db.ThingExistsAsync(Id, _repositoriesThingId).ConfigureAwait(false))
				{
					string d = await Db.GetThingCopyAsync(Id, _repositoriesThingId).ConfigureAwait(false);
					if (d != null)
					{
						if (Cryptographer != null)
							d = Cryptographer.Decrypt(d);

						d = Obfuscator.DeObfuscate(d);

						_repositories = _serializer.Deserialize<ConcurrentDictionary<string, Repository>>(d);

						foreach (var repository in _repositories.Values)
							repository.InitializeAfterDeserialize(this);
					}
				}

				Log.I($"{_repositories?.Values.Count} repositories loaded.", this);
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
				throw ex;
			}
			finally
			{
				_repositoriesLock.Release();
			}
		}

		public async Task SaveRepositoriesAsync()
		{
			await _repositoriesLock.WaitAsync().ConfigureAwait(false);
			try
			{
				string d = _serializer.Serialize(_repositories);

				d = Obfuscator.Obfuscate(d);

				if (Cryptographer != null)
					d = Cryptographer.Encrypt(d);

				await Db.StoreThingAsync(Id, _repositoriesThingId, d, DateTime.UtcNow).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
				// no rethrow!
			}
			finally
			{
				_repositoriesLock.Release();
			}
		}

		public async Task<Repository> NewRepositoryAsync(string name)
		{
			try
			{
				Repository repository = Repository.New(this, name);

				_repositories[repository.Id] = repository;

				await SaveRepositoriesAsync().ConfigureAwait(false);

				Log.I($"created new repository '{repository.Id}'.", this);

				return repository;
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
				throw ex;
			}
		}

		public async Task<Repository> GetRepositoryAsync(string repositoryId)
		{
			try
			{
				return _repositories[repositoryId];
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
				throw ex;
			}
		}

		public async Task RemoveRepositoryAsync(string repositoryId)
		{
			try
			{
				await Db.DiscardAllAsync(repositoryId);

				_repositories.TryRemove(repositoryId, out Repository r);

				await SaveRepositoriesAsync().ConfigureAwait(false);

				Log.I($"removed repository '{repositoryId}'.", this);
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
				throw ex;
			}
		}

		#endregion

		#region Apps

		public async Task<string> RegisterAppAsync(string repositoryId, string appPublicKey)
		{
			if (_repositories.TryGetValue(repositoryId, out Repository repository))
			{
				try
				{
					appPublicKey = BODY(appPublicKey);

					App app = _apps.Values.ToList().Find((_app) => (_app.PublicKey == appPublicKey && _app.Repository == repository));
					if (app == null)
					{
						app = new App(this, repository, appPublicKey);
						_apps[app.Token] = app;

						Log.I($"created new app '{app.Token}' for '{repository.Id}'.", this);
					}

					if (app != null)
						return OK(app.Token);
				}
				catch (Exception ex)
				{
					return Error($"1002,{ex.Message}");
				}

				return Error("1001,Failed to register application.");
			}

			return Error($"1000,Repository doesn't exist.");
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
					return Error($"2002,{ex.Message}");
				}

				return Error("2001,Failed to create/open storage.");
			}

			return Error($"2000,Incorrect application token.");
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
					return Error($"3001,{ex.Message}", callerName);
				}
			}

			return Error($"3000,Incorrect storage token.", callerName);
		}

		public async Task<string> StoreThingAsync(string storageToken, string thingId, string data)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				await storage.StoreAsync(thingId, BODY(data)).ConfigureAwait(false);
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

		string BODY(string data)
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
