using System.Data.SqlClient;

namespace pbXStorage.Repositories
{
	public class SqlServerFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			return new DbOnSDC(
				new SqlConnection(connectionString),
				new DbOnSDC.Options()
				{
					SqlForNTextDataType = "nvarchar(max)"
				});
		}
	}
}
