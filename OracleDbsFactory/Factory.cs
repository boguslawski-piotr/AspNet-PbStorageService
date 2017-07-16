using pbXNet;
using pbXStorage.Repositories;

namespace Oracle
{
	public class OracleDbFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			// First: install Oracle.ManagedDataAccess NuGet package
			// Second: uncomment line below
			//return new DbOnSDC(new SDCDatabase(new Oracle.DataAccess.Client.OracleConnection(connectionString)));
			return null;
		}
	}

	public class MySqlFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			return new DbOnSDC(new SDCDatabase(new MySql.Data.MySqlClient.MySqlConnection(connectionString)));
		}
	}
}
