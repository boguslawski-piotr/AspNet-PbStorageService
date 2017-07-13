using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;

namespace pbXStorage.Server.NETCore.Data
{
	public class RepositoriesDbPool : IDisposable
	{
		ConcurrentQueue<IDb> _pool = new ConcurrentQueue<IDb>();
		int _maxPoolSize;

		DbContextOptions _options;

		public RepositoriesDbPool(DbContextOptions options)
		{
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_maxPoolSize = _options.FindExtension<CoreOptionsExtension>()?.MaxPoolSize ?? 128;
		}

		public void Dispose()
		{
			while (_pool.TryDequeue(out IDb db))
				db.Dispose();

			_options = null;
			_pool.Clear();
			_pool = null;
		}

		public virtual IDb Create()
		{
			var dbOnFileSystem = _options.FindExtension<DbOnFileSystemOptionsExtension>();
			if (dbOnFileSystem != null)
				return new DbOnFileSystem(dbOnFileSystem.Directory);
			else
			{
				var sqlite = _options.FindExtension<SqliteOptionsExtension>();
				if (sqlite != null)
					return new DbOnSDC(new SqliteConnection(sqlite.ConnectionString));
				else
				{
					var sqlserver = _options.FindExtension<SqlServerOptionsExtension>();
					if (sqlserver != null)
						return new DbOnSDC(new SqlConnection(sqlserver.ConnectionString));
				}

				throw new Exception("The database connection for repositories was not defined.");
			}
		}

		public IDb Rent()
		{
			if (_pool.TryDequeue(out IDb db))
				return db;

			return Create();
		}

		public void Return(IDb db)
		{
			if (db != null)
			{
				if (_pool.Count < _maxPoolSize)
					_pool.Enqueue(db);
				else
				{
					db.Dispose();
				}
			}
		}
	}
}
