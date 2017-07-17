using System.Data.SqlClient;
using pbXNet;
using pbXNet.Database;

namespace pbXStorage.Repositories
{
	class SqlServerSqlBuilder : SqlBuilder
	{
		protected SqlServerSqlBuilder(SqlServerSqlBuilder src)
			: base(src)
		{ }

		public override SqlBuilder New() => new SqlServerSqlBuilder();
		public override SqlBuilder Clone() => new SqlServerSqlBuilder(this);

		public override string TextTypeName => "varchar(max)";
		public override string NTextTypeName => "nvarchar(max)";

		public override bool DropIndexStmtNeedsTableName => true;
	}

	public class SqlServerFactory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			return new DbOnSDC(
				new SDCDatabase(
					new SqlConnection(connectionString),
					new SqlServerSqlBuilder()
				)
			);
		}
	}
}
