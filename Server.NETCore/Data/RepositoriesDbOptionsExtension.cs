using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace pbXStorage.Server.NETCore.Data
{
	class RepositoriesDbOptionsExtension : IDbContextOptionsExtension
	{
		public const string SQliteProvider = "sqlite";
		public const string SqlServerProvider = "sqlserver";
		public const string DbOnFileSystemProvider = "dbonfilesystem";

		public string Provider;
		public string ConnectionString;

		public RepositoriesDbOptionsExtension(string provider, string connectionString)
		{
			Provider = provider;
			ConnectionString = connectionString;
		}

		public void ApplyServices(IServiceCollection services)
		{
			if (Provider != null)
				services.AddSingleton(new DbOnFileSystem(ConnectionString));
		}
	}
}
