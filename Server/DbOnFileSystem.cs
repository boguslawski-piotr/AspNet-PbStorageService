using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Server
{
	public class DbOnFileSystem : IDb
	{
		public ISimpleCryptographer Cryptographer { get; set; }

		IFileSystem _fs;

		ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

		public DbOnFileSystem(string directory = null)
		{
			_fs = new DeviceFileSystem(DeviceFileSystemRoot.UserDefined, directory ?? "~");

			Log.I($"Data will be stored in directory: '{directory}'.", this);
		}

		async Task<IFileSystem> GetFs(string storageId)
		{
			IFileSystem fs = await _fs.CloneAsync();

			if (!string.IsNullOrWhiteSpace(storageId))
			{
				storageId = storageId.Replace('/', Path.DirectorySeparatorChar);
				await fs.CreateDirectoryAsync(storageId).ConfigureAwait(false);
			}

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

		public async Task StoreThingAsync(string storageId, string thingId, string data, DateTime modifiedOn)
		{
			await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				if (Cryptographer != null)
					data = Cryptographer.Encrypt(data);

				await fs.WriteTextAsync(thingId, data).ConfigureAwait(false);
				await fs.SetFileModifiedOnAsync(thingId, modifiedOn).ConfigureAwait(false);
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
				if (!await fs.FileExistsAsync(thingId).ConfigureAwait(false))
					throw new Exception($"'{storageId}/{thingId}' was not found.");

				DateTime modifiedOn = await fs.GetFileModifiedOnAsync(thingId).ConfigureAwait(false);
				return modifiedOn.ToBinary().ToString();
			})
			.ConfigureAwait(false);

			return DateTime.FromBinary(long.Parse(rc));
		}

		public async Task<string> GetThingCopyAsync(string storageId, string thingId)
		{
			return await ExecuteInLock(storageId, thingId, async (IFileSystem fs) =>
			{
				if (!await fs.FileExistsAsync(thingId).ConfigureAwait(false))
					throw new Exception($"'{storageId}/{thingId}' was not found.");

				string data = await fs.ReadTextAsync(thingId).ConfigureAwait(false);

				if (Cryptographer != null)
					data = Cryptographer.Decrypt(data);

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

		public async Task<IEnumerable<IdInDb>> FindThingIdsAsync(string storageId, string pattern)
		{
			IFileSystem fs = await GetFs(storageId).ConfigureAwait(false);
			IEnumerable<string> ids = await fs.GetFilesAsync(pattern).ConfigureAwait(false);
			storageId = storageId.Replace(Path.DirectorySeparatorChar, '/');
			return ids.Select<string, IdInDb>((id) => new IdInDb { Type = IdInDbType.Thing, Id = id, StorageId = storageId });
		}

		public async Task DiscardAllAsync(string storageId)
		{
			if (string.IsNullOrWhiteSpace(storageId))
				return;

			async Task InternalDiscardAllAsync(string _storageId)
			{
				IFileSystem _fs = await GetFs(_storageId).ConfigureAwait(false);

				foreach (var sid in await _fs.GetDirectoriesAsync().ConfigureAwait(false))
				{
					await InternalDiscardAllAsync(Path.Combine(_storageId, sid)).ConfigureAwait(false);
					await _fs.DeleteDirectoryAsync(sid).ConfigureAwait(false);
				}

				foreach (var tid in await _fs.GetFilesAsync().ConfigureAwait(false))
				{
					SemaphoreSlim _lock = GetLock(_fs, tid);
					await _lock.WaitAsync().ConfigureAwait(false);
					try
					{
						await _fs.DeleteFileAsync(tid).ConfigureAwait(false);
					}
					finally
					{
						_lock.Release();
					}
				}
			}

			// Discard all inside storage...
			await InternalDiscardAllAsync(storageId).ConfigureAwait(false);

			// Discard storage directory...
			string[] sids = storageId.Split('/');
			if (sids.Length > 0)
			{
				IFileSystem fs = await GetFs(sids.Length > 1 ? sids[0] : null).ConfigureAwait(false);
				await fs.DeleteDirectoryAsync(sids[sids.Length - 1]).ConfigureAwait(false);
			}
		}

		public async Task<IEnumerable<IdInDb>> FindAllIdsAsync(string storageId, string pattern)
		{
			IFileSystem fs = await GetFs(storageId).ConfigureAwait(false);
			List<IdInDb> ids = new List<IdInDb>();

			foreach (var sid in await fs.GetDirectoriesAsync().ConfigureAwait(false))
			{
				var _ids = await FindAllIdsAsync(Path.Combine(storageId, sid), pattern).ConfigureAwait(false);
				ids.AddRange(_ids);
				if (_ids.Any() || Regex.IsMatch(sid, pattern))
					ids.Add(new IdInDb { Type = IdInDbType.Storage, Id = sid, StorageId = storageId.Replace(Path.DirectorySeparatorChar, '/') });
			}

			ids.AddRange(await FindThingIdsAsync(storageId, pattern).ConfigureAwait(false));

			return ids;
		}
	}
}