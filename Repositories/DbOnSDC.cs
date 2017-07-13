using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Repositories
{
	class DbOnSDC : IDb
	{
		public class Options
		{
			public string SqlForNTextDataType = "ntext";
			public string SqlForNVarcharDataType = "nvarchar(?)";
		}

		Options _options;
		DbConnection _db;
		bool _closeDb;

		public DbOnSDC(DbConnection db, Options options = null)
		{
			_db = db;
			_options = options ?? new Options();
		}

		public virtual void Dispose()
		{
			if (_closeDb)
				_db.Close();

			_db = null;
			_closeDb = false;
		}

		public virtual async Task OpenAsync()
		{
			if (_db.State == System.Data.ConnectionState.Broken)
				_db.Close();
			if (_db.State == System.Data.ConnectionState.Closed)
			{
				await _db.OpenAsync().ConfigureAwait(false);
				_closeDb = true;

				Log.I($"opened connection to database '{_db.DataSource}/{_db.Database}'.", this);
			}
			if (_db.State != System.Data.ConnectionState.Open)
			{
				await Task.Delay(1000).ConfigureAwait(false);
				if (_db.State != System.Data.ConnectionState.Open)
					throw new Exception($"Unable to connect to database '{_db.DataSource}/{_db.Database}'.");
			}
		}

		protected enum CommandType
		{
			Statement,
			Scalar,
			Query
		};

		protected virtual async Task<object> ExecuteCommandAsync(CommandType type, string sql, params (string name, object value)[] args)
		{
			await OpenAsync();

			using (DbCommand cmd = _db.CreateCommand())
			{
				cmd.CommandText = sql;
				if (args?.Length > 0)
				{
					foreach (var arg in args)
					{
						DbParameter p = cmd.CreateParameter();
						p.ParameterName = arg.name;
						p.Value = arg.value;
						cmd.Parameters.Add(p);
					}
				}

#if DEBUG
				string dsql = sql;
				foreach (DbParameter p in cmd.Parameters)
					dsql = dsql.Replace($"@{p.ParameterName}", $"{{{p.Value.ToString()}}}");
				Log.D($"{type}: {dsql}", this);
#endif

				if (type == CommandType.Statement)
					return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
				else if (type == CommandType.Scalar)
					return await cmd.ExecuteScalarAsync().ConfigureAwait(false);
				else
					return await cmd.ExecuteReaderAsync().ConfigureAwait(false);
			}
		}

		public async Task<int> StatementAsync(string sql, params (string name, object value)[] args) => (int)await ExecuteCommandAsync(CommandType.Statement, sql, args).ConfigureAwait(false);

		public async Task<object> ScalarAsync(string sql, params (string name, object value)[] args) => await ExecuteCommandAsync(CommandType.Scalar, sql, args).ConfigureAwait(false);

		public async Task<DbDataReader> QueryAsync(string sql, params (string name, object value)[] args) => (DbDataReader)await ExecuteCommandAsync(CommandType.Query, sql, args).ConfigureAwait(false);

		public async Task CreateAsync()
		{
			bool thingsTableExists = true;
			try
			{
				await ScalarAsync("SELECT count(*) FROM Things;").ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				thingsTableExists = false;
			}

			if (!thingsTableExists)
			{
				string SqlForNVarcharDataType(int maxLength) => _options.SqlForNVarcharDataType.Replace("?", maxLength.ToString());

				await StatementAsync(
					"CREATE TABLE \"Things\" " +
						$"(\"StorageId\" {SqlForNVarcharDataType(512)} NOT NULL, " +
						$"\"Id\" {SqlForNVarcharDataType(256)} NOT NULL, " +
						$"\"Data\" {_options.SqlForNTextDataType} NULL, " +
						"\"ModifiedOn\" bigint NOT NULL, " +
						"CONSTRAINT \"PK_Things\" PRIMARY KEY (\"StorageId\", \"Id\"));"
				).ConfigureAwait(false);

				await StatementAsync(
					"CREATE INDEX \"IX_Things_StorageId\" ON \"Things\" (\"StorageId\");").ConfigureAwait(false);

				Log.I("table Things has been created.", this);
			}
		}

		public async Task StoreThingAsync(string storageId, string thingId, string data, DateTime modifiedOn, ISimpleCryptographer cryptographer = null)
		{
			if (cryptographer != null)
				data = cryptographer.Encrypt(data);

			(string, object)[] args = new(string, object)[] {
				("sid", storageId),
				("id", thingId),
				("d", data),
				("mon", modifiedOn.ToUniversalTime().ToBinary())
			};

			if (await ThingExistsAsync(storageId, thingId))
			{
				await StatementAsync("UPDATE Things SET Data = @d, ModifiedOn = @mon WHERe StorageId = @sid and Id = @id;", args).ConfigureAwait(false);
			}
			else
			{
				await StatementAsync("INSERT INTO Things (StorageId, Id, Data, ModifiedOn) VALUES (@sid, @id, @d, @mon);", args).ConfigureAwait(false);
			}
		}

		public async Task<bool> ThingExistsAsync(string storageId, string thingId)
		{
			object rc = await ScalarAsync("SELECT count(StorageId) FROM Things WHERE StorageId = @sid and Id = @id;", ("sid", storageId), ("id", thingId)).ConfigureAwait(false);
			return Convert.ToBoolean(rc);
		}

		public async Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId)
		{
			object rc = await ScalarAsync("SELECT ModifiedOn FROM Things WHERE StorageId = @sid and Id = @id;", ("sid", storageId), ("id", thingId)).ConfigureAwait(false);
			if (rc == null)
				throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));
			return DateTime.FromBinary(Convert.ToInt64(rc));
		}

		public async Task<string> GetThingCopyAsync(string storageId, string thingId, ISimpleCryptographer cryptographer = null)
		{
			object rc = await ScalarAsync("SELECT Data FROM Things WHERE StorageId = @sid and Id = @id;", ("sid", storageId), ("id", thingId)).ConfigureAwait(false);
			if (rc == null)
				throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));

			string data = Convert.ToString(rc);

			if (cryptographer != null)
				data = cryptographer.Decrypt(data);

			return data;
		}

		public async Task DiscardThingAsync(string storageId, string thingId)
		{
			if (await ThingExistsAsync(storageId, thingId))
			{
				await StatementAsync("DELETE FROM Things WHERE StorageId = @sid and Id = @id;", ("sid", storageId), ("id", thingId)).ConfigureAwait(false);
			}
		}

		public async Task<IEnumerable<IdInDb>> FindThingIdsAsync(string storageId, string pattern)
		{
			using (DbDataReader rows = await QueryAsync("SELECT StorageId, Id FROM Things WHERE StorageId = @sid;", ("sid", storageId)).ConfigureAwait(false))
			{
				bool emptyPattern = string.IsNullOrWhiteSpace(pattern);
				List<IdInDb> ids = new List<IdInDb>();

				foreach (DbDataRecord r in rows)
				{
					string id = r.GetString(1);
					if (emptyPattern || Regex.IsMatch(id, pattern))
						ids.Add(new IdInDb
						{
							StorageId = storageId,
							Type = IdInDbType.Thing,
							Id = id,
						});
				}

				return ids;
			}
		}

		public async Task DiscardAllAsync(string storageId)
		{
			await StatementAsync("DELETE FROM Things WHERE StorageId = @sid;", ("sid", storageId)).ConfigureAwait(false);
			if (storageId.IndexOf('/') < 0)
				await StatementAsync("DELETE FROM Things WHERE StorageId like @sid;", ("sid", storageId + "/%")).ConfigureAwait(false);

			// StartsWith:
			// select * from Things where (StorageId like "test" || '%' and (substr(StorageId, 1, length("test"))) = "test") or StorageId = ""
		}

		public async Task<IEnumerable<IdInDb>> FindAllIdsAsync(string storageId, string pattern)
		{
			string sql = "SELECT StorageId, Id FROM Things WHERE ";

			DbDataReader rows = null;
			if (storageId.IndexOf('/') < 0)
				rows = await QueryAsync(sql + "StorageId like @sid;", ("sid", storageId + "/%")).ConfigureAwait(false);
			else
				rows = await QueryAsync(sql + "StorageId = @sid;", ("sid", storageId)).ConfigureAwait(false);

			using (rows)
			{
				bool emptyPattern = string.IsNullOrWhiteSpace(pattern);
				List<IdInDb> ids = new List<IdInDb>();

				foreach (DbDataRecord r in rows)
				{
					string id = r.GetString(1);
					if (emptyPattern || Regex.IsMatch(id, pattern))
					{
						ids.Add(new IdInDb
						{
							StorageId = r.GetString(0),
							Type = IdInDbType.Thing,
							Id = id,
						});
					}
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
