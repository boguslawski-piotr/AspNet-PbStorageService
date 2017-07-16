using Microsoft.Data.Sqlite;
using pbXNet.Database;

namespace pbXStorage.Repositories
{
	public class SqliteFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			return new DbOnSDC(new SDCDatabase(new SqliteConnection(connectionString)));
		}
	}
}
