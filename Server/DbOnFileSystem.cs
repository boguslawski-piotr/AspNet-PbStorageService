using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class DbOnFileSystem : ManagedObject, IDb
	{
		public bool Initialized { get; private set;  }

		IFileSystem _fs;

		ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

		public DbOnFileSystem()
			: base(null)
		{
		}

		public DbOnFileSystem(Manager manager) 
			: base(manager)
		{
		}

		public Task InitializeAsync(Manager manager)
		{
			Manager = manager;

			string homePath = Environment.GetEnvironmentVariable("HOME");
			if (homePath == null)
				homePath = Environment.GetEnvironmentVariable("HOMEPATH");

			_fs = new DeviceFileSystem(DeviceFileSystemRoot.UserDefined, homePath);

			Initialized = true;

			return Task.FromResult(true);
		}

		async Task<IFileSystem> GetFs(Storage storage)
		{
			IFileSystem fs = await _fs.CloneAsync();

			await fs.CreateDirectoryAsync(".pbXStorage").ConfigureAwait(false);

			if (storage != null)
			{
				await fs.CreateDirectoryAsync(storage.App.Client.Id).ConfigureAwait(false);
				await fs.CreateDirectoryAsync(storage.Id).ConfigureAwait(false);
			}

			return fs;
		}


		const string _clientsFileName = ".clients";

		public async Task<string> GetClientsAsync()
		{
			IFileSystem fs = await GetFs(null).ConfigureAwait(false);
			if (await fs.FileExistsAsync(_clientsFileName).ConfigureAwait(false))
				return await fs.ReadTextAsync(_clientsFileName).ConfigureAwait(false);
			return null;
		}

		public async Task StoreClientsAsync(string clientsData)
		{
			IFileSystem fs = await GetFs(null);
			await fs.WriteTextAsync(_clientsFileName, clientsData);
		}


		string PrepareFileName(string thingId)
		{
			return Regex.Replace(thingId, "[\\/:*?<>|]", "-");
		}

		SemaphoreSlim GetLock(IFileSystem fs, string thingId)
		{
			string key = Path.Combine(fs.CurrentPath, thingId);

			if (!_locks.TryGetValue(key, out SemaphoreSlim _lock))
			{
				_lock = new SemaphoreSlim(1);
				_locks[key] = _lock;
			}

			return _lock;
		}

		async Task<string> ExecuteInLock(Storage storage, string thingId, Func<IFileSystem, Task<string>> action, [CallerMemberName]string callerName = null)
		{
			IFileSystem fs = await GetFs(storage).ConfigureAwait(false);
			SemaphoreSlim _lock = GetLock(fs, thingId);
			await _lock.WaitAsync().ConfigureAwait(false);
			try
			{
				Task<string> task = action(fs);
				return await task.ConfigureAwait(false);
			}
			finally
			{
				_lock.Release();
			}
		}

		public async Task StoreThingAsync(Storage storage, string thingId, string data)
		{
			await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				data = Manager != null && Manager.Encrypter != null ? Manager.Encrypter(data) : data;
				await fs.WriteTextAsync(thingId, data).ConfigureAwait(false);
				return null;
			})
			.ConfigureAwait(false);
		}

		public async Task<bool> ThingExistsAsync(Storage storage, string thingId)
		{
			string rc = await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				bool exists = await fs.FileExistsAsync(thingId).ConfigureAwait(false);
				return exists ? "YES" : "NO";
			})
			.ConfigureAwait(false);

			return rc == "YES";
		}

		public async Task<DateTime> GetThingModifiedOnAsync(Storage storage, string thingId)
		{
			string rc = await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				DateTime modifiedOn = await fs.GetFileModifiedOnAsync(thingId).ConfigureAwait(false);
				return modifiedOn.ToBinary().ToString();
			})
			.ConfigureAwait(false);

			return DateTime.FromBinary(long.Parse(rc));
		}

		public async Task SetThingModifiedOnAsync(Storage storage, string thingId, DateTime modifiedOn)
		{
			await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				await fs.SetFileModifiedOnAsync(thingId, modifiedOn).ConfigureAwait(false);
				return null;
			})
			.ConfigureAwait(false);
		}

		public async Task<string> GetThingCopyAsync(Storage storage, string thingId)
		{
			return await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				if (!await fs.FileExistsAsync(thingId).ConfigureAwait(false))
					throw new Exception($"'{storage.Id}/{thingId}' was not found.");

				string data = await fs.ReadTextAsync(thingId).ConfigureAwait(false);
				data = Manager != null && Manager.Decrypter != null ? Manager.Decrypter(data) : data;

				return data;
			})
			.ConfigureAwait(false);
		}

		public async Task DiscardThingAsync(Storage storage, string thingId)
		{
			await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				await fs.DeleteFileAsync(thingId).ConfigureAwait(false);
				return null;
			})
			.ConfigureAwait(false);
		}

		public async Task<IEnumerable<string>> FindThingIdsAsync(Storage storage, string pattern)
		{
			IFileSystem fs = await GetFs(storage).ConfigureAwait(false);
			return await fs.GetFilesAsync(pattern).ConfigureAwait(false);
		}
	}
}