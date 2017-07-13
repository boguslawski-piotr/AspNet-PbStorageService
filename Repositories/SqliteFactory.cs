using Microsoft.Data.Sqlite;

namespace pbXStorage.Repositories
{
	public class SqliteFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			return new DbOnSDC(new SqliteConnection(connectionString));
		}
	}
}
