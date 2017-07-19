using System.Data.SqlClient;
using pbXNet;
using pbXNet.Database;

namespace pbXStorage.Repositories
{
	public class SqlServerFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			return new DbOnSDC(
				new SDCDatabase(
					new SqlConnection(connectionString),
					new SDCDatabase.Options {
						SqlBuilder = new SqlServerSqlBuilder()
					}
				)
			);
		}
	}
}
