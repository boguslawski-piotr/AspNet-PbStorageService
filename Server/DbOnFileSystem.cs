using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class DbOnFileSystem : ManagedObject, IDb
	{
		public bool Initialized { get; private set; }

		string _directory;

		IFileSystem _fs;

		ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

		public DbOnFileSystem(string directory = null, Manager manager = null)
			: base(manager)
		{
			_directory = directory;
		}

		public async Task InitializeAsync()
		{
			_fs = new DeviceFileSystem(DeviceFileSystemRoot.UserDefined, _directory ?? "~");

			Initialized = true;

			Log.I($"Data will be stored in directory: '{_directory}'.", this);
		}

		async Task<IFileSystem> GetFs(string storageId)
		{
			IFileSystem fs = await _fs.CloneAsync();

			if (!string.IsNullOrWhiteSpace(storageId))
				await fs.CreateDirectoryAsync(storageId).ConfigureAwait(false);

			return fs;
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

		async Task<string> ExecuteInLock(string storageId, string thingId, Func<IFileSystem, Task<string>> action, [CallerMemberName]string callerName = null)
		{
			IFileSystem fs = await GetFs(storageId).ConfigureAwait(false);
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

		public async Task StoreThingAsync(string storageId, string thingId, string data)
		{
			await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				if (Manager != null)
					data = Manager.Cryptographer != null ? Manager.Cryptographer.Encrypt(data) : data;

				await fs.WriteTextAsync(thingId, data).ConfigureAwait(false);
				return null;
			})
			.ConfigureAwait(false);
		}

		public async Task<bool> ThingExistsAsync(string storageId, string thingId)
		{
			string rc = await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				bool exists = await fs.FileExistsAsync(thingId).ConfigureAwait(false);
				return exists ? "YES" : "NO";
			})
			.ConfigureAwait(false);

			return rc == "YES";
		}

		public async Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId)
		{
			string rc = await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				DateTime modifiedOn = await fs.GetFileModifiedOnAsync(thingId).ConfigureAwait(false);
				return modifiedOn.ToBinary().ToString();
			})
			.ConfigureAwait(false);

			return DateTime.FromBinary(long.Parse(rc));
		}

		public async Task SetThingModifiedOnAsync(string storageId, string thingId, DateTime modifiedOn)
		{
			await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				await fs.SetFileModifiedOnAsync(thingId, modifiedOn).ConfigureAwait(false);
				return null;
			})
			.ConfigureAwait(false);
		}

		public async Task<string> GetThingCopyAsync(string storageId, string thingId)
		{
			return await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				if (!await fs.FileExistsAsync(thingId).ConfigureAwait(false))
					throw new Exception($"'{storageId}/{thingId}' was not found.");

				string data = await fs.ReadTextAsync(thingId).ConfigureAwait(false);

				if (Manager != null)
					data = Manager.Cryptographer != null ? Manager.Cryptographer.Decrypt(data) : data;

				return data;
			})
			.ConfigureAwait(false);
		}

		public async Task DiscardThingAsync(string storageId, string thingId)
		{
			await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				await fs.DeleteFileAsync(thingId).ConfigureAwait(false);
				return null;
			})
			.ConfigureAwait(false);
		}

		public async Task<IEnumerable<string>> FindThingIdsAsync(string storageId, string pattern)
		{
			IFileSystem fs = await GetFs(storageId).ConfigureAwait(false);
			return await fs.GetFilesAsync(pattern).ConfigureAwait(false);
		}
	}
}