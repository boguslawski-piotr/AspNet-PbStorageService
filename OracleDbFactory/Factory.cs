using System;
using pbXStorage.Repositories;

namespace OracleDb
{
	public class Factory : IDbFactory
	{
		public IDb Create(string connectionString)
		{
			//return new DbOnFileSystem(connectionString);
			return null;
		}
	}
}
