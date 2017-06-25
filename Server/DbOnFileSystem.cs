using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class DbOnFileSystem : IDb
	{
		const string _clientsFileName = ".clients";

		IFileSystem _fs;

		ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

		public async Task InitializeAsync()
		{
			string homePath = Environment.GetEnvironmentVariable("HOME");
			if (homePath == null)
				homePath = Environment.GetEnvironmentVariable("HOMEPATH");

			_fs = new DeviceFileSystem(DeviceFileSystemRoot.UserDefined, homePath);
		}

		async Task<IFileSystem> GetFs(Storage storage)
		{
			IFileSystem fs = await _fs.CloneAsync();

			await fs.CreateDirectoryAsync(".pbXStorage");

			if (storage != null)
			{
				await fs.CreateDirectoryAsync(storage.App.Client.Id);
				await fs.CreateDirectoryAsync(storage.Id);
			}

			return fs;
		}


		public async Task<string> GetClientsAsync()
		{
			IFileSystem fs = await GetFs(null);
			if (await fs.FileExistsAsync(_clientsFileName))
				return await fs.ReadTextAsync(_clientsFileName);
			return null;
		}

		public async Task StoreClientsAsync(string clientsData)
		{
			IFileSystem fs = await GetFs(null);
			await fs.WriteTextAsync(_clientsFileName, clientsData);
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
			IFileSystem fs = await GetFs(storage);
			SemaphoreSlim _lock = GetLock(fs, thingId);
			await _lock.WaitAsync();
			try
			{
				Task<string> task = action(fs);
				return await task;
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
				await fs.WriteTextAsync(thingId, data);
				return null;
			});
		}

		public async Task<string> GetThingCopyAsync(Storage storage, string thingId)
		{
			return await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				if (!await fs.FileExistsAsync(thingId))
					throw new Exception($"'{storage.Id}/{thingId}' was not found.");
				return await fs.ReadTextAsync(thingId);
			});
		}

		public async Task DiscardThingAsync(Storage storage, string thingId)
		{
			await ExecuteInLock(storage, thingId, async (IFileSystem fs) =>
			{
				await fs.DeleteFileAsync(thingId);
				return null;
			});
		}
	}
}