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
	public class Context
	{
		public IDb RepositoriesDb { get; set; }
		public ISimpleCryptographer Cryptographer { get; set; }
	}

	public class Manager
	{
		public string Id { get; set; }

		ConcurrentDictionary<string, Repository> _repositories = new ConcurrentDictionary<string, Repository>();

		ConcurrentDictionary<string, App> _apps = new ConcurrentDictionary<string, App>();

		ConcurrentDictionary<string, Storage> _storages = new ConcurrentDictionary<string, Storage>();

		ISerializer _serializer { get; set; } = new NewtonsoftJsonSerializer();

		ISimpleCryptographer _cryptographer { get; set; }

		public Manager(string id, ISimpleCryptographer cryptographer = null, ISerializer serializer = null)
		{
			Id = id ?? throw new ArgumentException($"{nameof(Id)} must be valid object.");

			_cryptographer = cryptographer;

			if (serializer != null)
				_serializer = serializer;
		}

		public Context CreateContext(IDb repositoriesDb)
		{
			repositoriesDb.Cryptographer = _cryptographer;
			return new Context
			{
				RepositoriesDb = repositoriesDb,
				Cryptographer = _cryptographer,
			};
		}

		public async Task InitializeAsync(Context ctx)
		{
			await LoadRepositoriesAsync(ctx);
		}

		#region Repositories

		const string _repositoriesThingId = "c235e54b577c44418ab0948f299e830876b14f2cc39742f3bc4dd5ae581d1e31";

		readonly SemaphoreSlim _repositoriesLock = new SemaphoreSlim(1);

		public async Task LoadRepositoriesAsync(Context ctx)
		{
			await _repositoriesLock.WaitAsync().ConfigureAwait(false);
			try
			{
				if (await ctx.RepositoriesDb.ThingExistsAsync(Id, _repositoriesThingId).ConfigureAwait(false))
				{
					string d = await ctx.RepositoriesDb.GetThingCopyAsync(Id, _repositoriesThingId).ConfigureAwait(false);
					if (d != null)
					{
						if (ctx.Cryptographer != null)
							d = ctx.Cryptographer.Decrypt(d);

						d = Obfuscator.DeObfuscate(d);

						_repositories = _serializer.Deserialize<ConcurrentDictionary<string, Repository>>(d);

						foreach (var repository in _repositories.Values)
							repository.InitializeAfterDeserialize();
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

		public async Task SaveRepositoriesAsync(Context ctx)
		{
			await _repositoriesLock.WaitAsync().ConfigureAwait(false);
			try
			{
				string d = _serializer.Serialize(_repositories);

				d = Obfuscator.Obfuscate(d);

				if (ctx.Cryptographer != null)
					d = ctx.Cryptographer.Encrypt(d);

				await ctx.RepositoriesDb.StoreThingAsync(Id, _repositoriesThingId, d, DateTime.UtcNow).ConfigureAwait(false);
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

		public async Task<Repository> NewRepositoryAsync(Context ctx, string name)
		{
			try
			{
				Repository repository = Repository.New(name);

				_repositories[repository.Id] = repository;

				await SaveRepositoriesAsync(ctx).ConfigureAwait(false);

				Log.I($"created new repository '{repository.Id}'.", this);

				return repository;
			}
			catch (Exception ex)
			{
				Log.E(ex.Message, this);
				throw ex;
			}
		}

		public async Task<Repository> GetRepositoryAsync(Context ctx, string repositoryId)
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

		public async Task RemoveRepositoryAsync(Context ctx, string repositoryId)
		{
			try
			{
				await ctx.RepositoriesDb.DiscardAllAsync(repositoryId);

				_repositories.TryRemove(repositoryId, out Repository r);

				await SaveRepositoriesAsync(ctx).ConfigureAwait(false);

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

		public async Task<string> RegisterAppAsync(Context ctx, string repositoryId, string appPublicKey)
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
					return ERROR($"1002,{ex.Message}");
				}

				return ERROR("1001,Failed to register application.");
			}

			return ERROR($"1000,Repository doesn't exist.");
		}

		#endregion

		#region Storages

		public async Task<string> OpenStorageAsync(Context ctx, string appToken, string storageId)
		{
			if (_apps.TryGetValue(appToken, out App app))
			{
				try
				{
					Storage storage = _storages.Values.ToList().Find((_storage) => (_storage.App.Token == appToken && _storage.Id == storageId));
					if (storage == null)
					{
						storage = new Storage(app, storageId);
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
					return ERROR($"2002,{ex.Message}");
				}

				return ERROR("2001,Failed to create/open storage.");
			}

			return ERROR($"2000,Incorrect application token.");
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
					return ERROR($"3001,{ex.Message}", callerName);
				}
			}

			return ERROR($"3000,Incorrect storage token.", callerName);
		}

		public async Task<string> StoreThingAsync(Context ctx, string storageToken, string thingId, string data)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				await storage.StoreAsync(ctx.RepositoriesDb, thingId, BODY(data)).ConfigureAwait(false);
				return OK();
			}).ConfigureAwait(false);
		}

		public async Task<string> ThingExistsAsync(Context ctx, string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.ExistsAsync(ctx.RepositoriesDb, thingId).ConfigureAwait(false));
			}).ConfigureAwait(false);
		}

		public async Task<string> GetThingModifiedOnAsync(Context ctx, string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.GetModifiedOnAsync(ctx.RepositoriesDb, thingId).ConfigureAwait(false));
			}).ConfigureAwait(false);
		}

		public async Task<string> GetThingCopyAsync(Context ctx, string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.GetACopyAsync(ctx.RepositoriesDb, thingId).ConfigureAwait(false));
			}).ConfigureAwait(false);
		}

		public async Task<string> DiscardThingAsync(Context ctx, string storageToken, string thingId)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				await storage.DiscardAsync(ctx.RepositoriesDb, thingId).ConfigureAwait(false);
				return OK();
			}).ConfigureAwait(false);
		}

		public async Task<string> FindThingIdsAsync(Context ctx, string storageToken, string pattern)
		{
			return await ExecuteInStorage(storageToken, async (Storage storage) =>
			{
				return OK(await storage.FindIdsAsync(ctx.RepositoriesDb, pattern).ConfigureAwait(false));
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

		string ERROR(string message, [CallerMemberName]string callerName = null)
		{
			Log.E(message, this, callerName);
			return Obfuscator.Obfuscate($"ERROR,{message}");
		}

		#endregion
	}
}
