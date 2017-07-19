using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pbXNet;
using pbXNet.Database;

namespace pbXStorage.Repositories
{
	public class DbOnSDC : IDb
	{
		const string TableName = "pbXStorage_Things";

		class Thing
		{
			[PrimaryKey]
			[Length(192)]
			[Index(nameof(StorageId))]
			public string StorageId { get; set; }

			[PrimaryKey]
			[Length(192)]
			public string Id { get; set; }

			[Length(int.MaxValue)]
			public string Data { get; set; }

			[NotNull]
			public long ModifiedOn { get; set; }
		}

		IDatabase _db;
		ITable<Thing> _things;

		public DbOnSDC(IDatabase db)
		{
			_db = db;
		}

		public virtual void Dispose()
		{
			_db?.Dispose();
			_db = null;
		}

		public async Task CreateAsync()
		{
			//await _db.DropTableAsync(TableName);
			_things = await _db.TableAsync<Thing>(TableName);
		}

		Thing PrepareThingPk(string storageId, string thingId)
		{
			return new Thing
			{
				StorageId = storageId,
				Id = thingId,
			};
		}

		public async Task StoreThingAsync(string storageId, string thingId, string data, DateTime modifiedOn, ISimpleCryptographer cryptographer = null)
		{
			if (cryptographer != null)
				data = cryptographer.Encrypt(data);

			Thing thing = PrepareThingPk(storageId, thingId);
			thing.Data = data;
			thing.ModifiedOn = modifiedOn.ToUniversalTime().Ticks;

			await _things.InsertOrUpdateAsync(thing).ConfigureAwait(false);
		}

		async Task<Thing> GetThingRawCopyAsync(string storageId, string thingId)
		{
			return await _things.FindAsync(PrepareThingPk(storageId, thingId)).ConfigureAwait(false);
		}

		public async Task<bool> ThingExistsAsync(string storageId, string thingId)
		{
			return await _things.ExistsAsync(PrepareThingPk(storageId, thingId)).ConfigureAwait(false);
		}

		public async Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId)
		{
			Thing thing = await GetThingRawCopyAsync(storageId, thingId).ConfigureAwait(false) ?? throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));

			return new DateTime(thing.ModifiedOn, DateTimeKind.Utc);
		}

		public async Task<string> GetThingCopyAsync(string storageId, string thingId, ISimpleCryptographer cryptographer = null)
		{
			Thing thing = await GetThingRawCopyAsync(storageId, thingId).ConfigureAwait(false) ?? throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));

			if (cryptographer != null)
				thing.Data = cryptographer.Decrypt(thing.Data);

			return thing.Data;
		}

		public async Task DiscardThingAsync(string storageId, string thingId)
		{
			await _things.DeleteAsync(PrepareThingPk(storageId, thingId)).ConfigureAwait(false);
		}

		public async Task<IEnumerable<IdInDb>> FindThingIdsAsync(string storageId, string pattern)
		{
			using (IQueryResult<Thing> q = await _things.Rows.Where(r => r.StorageId == storageId).QueryAsync().ConfigureAwait(false))
			{
				bool emptyPattern = string.IsNullOrWhiteSpace(pattern);
				List<IdInDb> ids = new List<IdInDb>();

				foreach (var r in q.Where(_r => emptyPattern || Regex.IsMatch(_r.Id, pattern)))
				{
					ids.Add(new IdInDb
					{
						StorageId = storageId,
						Type = IdInDbType.Thing,
						Id = r.Id,
					});
				}

				return ids;
			}
		}

		public async Task DiscardAllAsync(string storageId)
		{
			await _things.DeleteAsync(r => r.StorageId == storageId).ConfigureAwait(false);
			if (storageId.IndexOf('/') < 0)
				await _things.DeleteAsync(r => r.StorageId.StartsWith(storageId + "/")).ConfigureAwait(false);
		}

		public async Task<IEnumerable<IdInDb>> FindAllIdsAsync(string storageId, string pattern)
		{
			IQueryResult<Thing> q = null;

			if (storageId.IndexOf('/') < 0)
				q = await _things.Rows.Where(r => r.StorageId.StartsWith(storageId)).QueryAsync().ConfigureAwait(false);
			else
				q = await _things.Rows.Where(r => r.StorageId == storageId).QueryAsync().ConfigureAwait(false);

			using (q)
			{
				bool emptyPattern = string.IsNullOrWhiteSpace(pattern);
				List<IdInDb> ids = new List<IdInDb>();

				foreach (var r in q.Where(_r => emptyPattern || Regex.IsMatch(_r.Id, pattern)))
				{
					ids.Add(new IdInDb
					{
						StorageId = r.StorageId,
						Type = IdInDbType.Thing,
						Id = r.Id,
					});
				}

				IEnumerable<IGrouping<string, IdInDb>> gids = ids.GroupBy<IdInDb, string>(id => id.StorageId);

				var sids = gids.Select((IGrouping<string, IdInDb> s) =>
				{
					string[] _sids = s.Key.Split('/');
					return new IdInDb { Type = IdInDbType.Storage, Id = _sids[_sids.Length - 1], StorageId = _sids[0] };
				});

				ids.AddRange(sids);

				return ids;
			}
		}
	}
}
