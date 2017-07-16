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

		SDCDatabase _db;

		public DbOnSDC(SDCDatabase db)
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
			ITable<Thing> _things = await _db.CreateTableAsync<Thing>(TableName);
		}

		public async Task StoreThingAsync(string storageId, string thingId, string data, DateTime modifiedOn, ISimpleCryptographer cryptographer = null)
		{
			if (cryptographer != null)
				data = cryptographer.Encrypt(data);

			object[] parameters = new object[] {
				storageId,
				thingId,
				data,
				modifiedOn.ToUniversalTime().Ticks,
			};

			if (await ThingExistsAsync(storageId, thingId))
			{
				await _db.StatementAsync(_db.SqlBuilder
					.Update(TableName)
						["Data"].P(3)
						["ModifiedOn"].P(4)
					.Where
						["StorageId"].Eq.P(1)
						.And
						["Id"].Eq.P(2),
					parameters
				)
				.ConfigureAwait(false);
			}
			else
			{
				await _db.StatementAsync(_db.SqlBuilder
					.InsertInto(TableName)["StorageId"]["Id"]["Data"]["ModifiedOn"]
					.Values.P(1).P(2).P(3).P(4),
					parameters
				)
				.ConfigureAwait(false);
			}
		}

		public async Task<bool> ThingExistsAsync(string storageId, string thingId)
		{
			return Convert.ToBoolean(
				await _db.ScalarAsync<object>("SELECT 1 FROM Things WHERE StorageId = @_1 and Id = @_2;", storageId, thingId).ConfigureAwait(false)
			);
		}

		public async Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId)
		{
			object rc = await _db.ScalarAsync<object>("SELECT ModifiedOn FROM Things WHERE StorageId = @_1 and Id = @_2;", storageId, thingId).ConfigureAwait(false);
			if (rc == null)
				throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));
			return new DateTime(Convert.ToInt64(rc), DateTimeKind.Utc);
		}

		public async Task<string> GetThingCopyAsync(string storageId, string thingId, ISimpleCryptographer cryptographer = null)
		{
			string data = await _db.ScalarAsync<string>("SELECT Data FROM Things WHERE StorageId = @_1 and Id = @_2;", storageId, thingId).ConfigureAwait(false);
			if (data == null)
				throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));

			if (cryptographer != null)
				data = cryptographer.Decrypt(data);

			return data;
		}

		public async Task DiscardThingAsync(string storageId, string thingId)
		{
			if (await ThingExistsAsync(storageId, thingId))
			{
				await _db.StatementAsync(_db.SqlBuilder
					.Delete.From(TableName).Where["StorageId"].Eq.P(1).And["Id"].Eq.P(2),
					storageId, thingId
				)
				.ConfigureAwait(false);
			}
		}

		public async Task<IEnumerable<IdInDb>> FindThingIdsAsync(string storageId, string pattern)
		{
			using (IQuery<Thing> q = await _db.QueryAsync<Thing>("SELECT StorageId, Id FROM Things WHERE StorageId = @_1;", storageId).ConfigureAwait(false))
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
			await _db.StatementAsync("DELETE FROM Things WHERE StorageId = @_1;", storageId).ConfigureAwait(false);
			if (storageId.IndexOf('/') < 0)
				await _db.StatementAsync("DELETE FROM Things WHERE StorageId like @_1;", storageId + "/%").ConfigureAwait(false);

			// StartsWith:
			// select * from Things where (StorageId like "test" || '%' and (substr(StorageId, 1, length("test"))) = "test") or StorageId = ""
		}

		public async Task<IEnumerable<IdInDb>> FindAllIdsAsync(string storageId, string pattern)
		{
			SqlBuilder sql = _db.SqlBuilder
				.Select["StorageId"]["Id"].From(TableName)
				.Where;

			IQuery<Thing> q = null;
			if (storageId.IndexOf('/') < 0)
				q = await _db.QueryAsync<Thing>(sql["StorageId"].Like.P(1), storageId + "/%").ConfigureAwait(false);
			else
				q = await _db.QueryAsync<Thing>(sql["StorageId"].Eq.P(1), storageId).ConfigureAwait(false);

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
