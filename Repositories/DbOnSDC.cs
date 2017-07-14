using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using pbXNet;

namespace pbXStorage.Repositories
{
	public class DbOnSDC : IDb
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

		public virtual async Task CloseAsync()
		{
			if (_closeDb)
				_db.Close();
			_closeDb = false;
		}

		protected enum CommandType
		{
			Statement,
			Scalar,
			Query
		};

#if DEBUG
		void DumpParameters(DbCommand cmd, [CallerMemberName]string callerName = null)
		{
			string s = "";
			foreach (DbParameter p in cmd.Parameters)
				s += (s == "" ? "" : ", ") +
					$"@{p.ParameterName} = {{{p.Value.ToString()}}}";
			Log.D(s, this, callerName);
		}
#endif

		protected virtual void CreateParameters(DbCommand cmd, params (string name, object value)[] args)
		{
			foreach (var arg in args)
			{
				DbParameter p = cmd.CreateParameter();
				p.ParameterName = arg.name;
				p.Value = arg.value;
				cmd.Parameters.Add(p);
			}
#if DEBUG
			DumpParameters(cmd);
#endif
		}

		protected DbCommand CreateCommand(CommandType type, string sql, params (string name, object value)[] args)
		{
			DbCommand cmd = CreateCommand(type, sql);
			CreateParameters(cmd, args);
			return cmd;
		}

		protected DbCommand CreateCommand(CommandType type, string sql, params object[] args)
		{
			(string name, object value)[] _args = new (string name, object value)[args.Length];

			for (int i = 0; i < args.Length; i++)
				_args[i] = ($"_{i + 1}", args[i]);

			return CreateCommand(type, sql, _args);
		}

		protected virtual DbCommand CreateCommand(CommandType type, string sql)
		{
			DbCommand cmd = _db.CreateCommand();
			cmd.CommandText = sql;

			Log.D($"{type}: {sql}", this);

			return cmd;
		}

		public class QueryResult : IDisposable
		{
			public DbCommand Cmd;
			public DbDataReader Rows;

			public void Dispose()
			{
				Rows?.Dispose();
				Rows = null;
				Cmd?.Dispose();
				Cmd = null;
			}
		}

		protected async Task<object> ExecuteCommandAsync(CommandType type, string sql, params (string name, object value)[] args) => await ExecuteCommandAsync(type, CreateCommand(type, sql, args), true);
		protected async Task<object> ExecuteCommandAsync(CommandType type, string sql, params object[] args) => await ExecuteCommandAsync(type, CreateCommand(type, sql, args), true);
		protected async Task<object> ExecuteCommandAsync(CommandType type, string sql) => await ExecuteCommandAsync(type, CreateCommand(type, sql), true);
		protected async Task<object> ExecuteCommandAsync(CommandType type, DbCommand cmd) => await ExecuteCommandAsync(type, cmd, false);

		protected virtual async Task<object> ExecuteCommandAsync(CommandType type, DbCommand cmd, bool shouldDisposeCmd)
		{
			await OpenAsync();
			try
			{
				if (type == CommandType.Statement)
					return await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
				else if (type == CommandType.Scalar)
					return await cmd.ExecuteScalarAsync().ConfigureAwait(false);
				else
				{
					return new QueryResult()
					{
						Rows = await cmd.ExecuteReaderAsync().ConfigureAwait(false),
						Cmd = shouldDisposeCmd ? cmd : null,
					};
				}
			}
			finally
			{
				if (shouldDisposeCmd && type != CommandType.Query)
					cmd.Dispose();
			}
		}

		public async Task<int> StatementAsync(string sql, params (string name, object value)[] args) => (int)await ExecuteCommandAsync(CommandType.Statement, sql, args).ConfigureAwait(false);
		public async Task<int> StatementAsync(string sql, params object[] args) => (int)await ExecuteCommandAsync(CommandType.Statement, sql, args).ConfigureAwait(false);
		public async Task<int> StatementAsync(string sql) => (int)await ExecuteCommandAsync(CommandType.Statement, sql).ConfigureAwait(false);

		public async Task<object> ScalarAsync(string sql, params (string name, object value)[] args) => await ExecuteCommandAsync(CommandType.Scalar, sql, args).ConfigureAwait(false);
		public async Task<object> ScalarAsync(string sql, params object[] args) => await ExecuteCommandAsync(CommandType.Scalar, sql, args).ConfigureAwait(false);
		public async Task<object> ScalarAsync(string sql) => await ExecuteCommandAsync(CommandType.Scalar, sql).ConfigureAwait(false);

		public async Task<QueryResult> QueryAsync(string sql, params (string name, object value)[] args) => (QueryResult)await ExecuteCommandAsync(CommandType.Query, sql, args).ConfigureAwait(false);
		public async Task<QueryResult> QueryAsync(string sql, params object[] args) => (QueryResult)await ExecuteCommandAsync(CommandType.Query, sql, args).ConfigureAwait(false);
		public async Task<QueryResult> QueryAsync(string sql) => (QueryResult)await ExecuteCommandAsync(CommandType.Query, sql).ConfigureAwait(false);

		public async Task CreateAsync()
		{
			bool thingsTableExists = true;
			try
			{
				await ScalarAsync("SELECT 1 FROM Things;").ConfigureAwait(false);
			}
			catch
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

			object[] args = new object[] {
				storageId,
				thingId,
				data,
				modifiedOn.ToUniversalTime().ToBinary(),
			};

			if (await ThingExistsAsync(storageId, thingId))
			{
				await StatementAsync("UPDATE Things SET Data = @_3, ModifiedOn = @_4 WHERE StorageId = @_1 and Id = @_2;", args).ConfigureAwait(false);
			}
			else
			{
				await StatementAsync("INSERT INTO Things (StorageId, Id, Data, ModifiedOn) VALUES (@_1, @_2, @_3, @_4);", args).ConfigureAwait(false);
			}
		}

		public async Task<bool> ThingExistsAsync(string storageId, string thingId)
		{
			object rc = await ScalarAsync("SELECT 1 FROM Things WHERE StorageId = @_1 and Id = @_2;", storageId, thingId).ConfigureAwait(false);
			return Convert.ToBoolean(rc);
		}

		public async Task<DateTime> GetThingModifiedOnAsync(string storageId, string thingId)
		{
			object rc = await ScalarAsync("SELECT ModifiedOn FROM Things WHERE StorageId = @_1 and Id = @_2;", storageId, thingId).ConfigureAwait(false);
			if (rc == null)
				throw new Exception(T.Localized("PXS_ThingNotFound", storageId, thingId));
			return DateTime.FromBinary(Convert.ToInt64(rc));
		}

		public async Task<string> GetThingCopyAsync(string storageId, string thingId, ISimpleCryptographer cryptographer = null)
		{
			object rc = await ScalarAsync("SELECT Data FROM Things WHERE StorageId = @_1 and Id = @_2;", storageId, thingId).ConfigureAwait(false);
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
				await StatementAsync("DELETE FROM Things WHERE StorageId = @_1 and Id = @_2;", storageId, thingId).ConfigureAwait(false);
			}
		}

		public async Task<IEnumerable<IdInDb>> FindThingIdsAsync(string storageId, string pattern)
		{
			using (QueryResult q = await QueryAsync("SELECT StorageId, Id FROM Things WHERE StorageId = @_1;", storageId).ConfigureAwait(false))
			{
				bool emptyPattern = string.IsNullOrWhiteSpace(pattern);
				List<IdInDb> ids = new List<IdInDb>();

				foreach (DbDataRecord r in q.Rows)
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
			await StatementAsync("DELETE FROM Things WHERE StorageId = @_1;", storageId).ConfigureAwait(false);
			if (storageId.IndexOf('/') < 0)
				await StatementAsync("DELETE FROM Things WHERE StorageId like @_1;", storageId + "/%").ConfigureAwait(false);

			// StartsWith:
			// select * from Things where (StorageId like "test" || '%' and (substr(StorageId, 1, length("test"))) = "test") or StorageId = ""
		}

		public async Task<IEnumerable<IdInDb>> FindAllIdsAsync(string storageId, string pattern)
		{
			string sql = "SELECT StorageId, Id FROM Things WHERE ";

			QueryResult q = null;
			if (storageId.IndexOf('/') < 0)
				q = await QueryAsync(sql + "StorageId like @_1;", storageId + "/%").ConfigureAwait(false);
			else
				q = await QueryAsync(sql + "StorageId = @_1;", storageId).ConfigureAwait(false);
			
			using (q)
			{
				bool emptyPattern = string.IsNullOrWhiteSpace(pattern);
				List<IdInDb> ids = new List<IdInDb>();

				foreach (DbDataRecord r in q.Rows)
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
