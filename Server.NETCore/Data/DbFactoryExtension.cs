using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace pbXStorage.Server.AspNetCore.Data
{
	public class DbFactoryExtension : IDbContextOptionsExtension
	{
		public string ConnectionString { get; private set; }
		public IDbFactory Factory { get; private set; }

		public DbFactoryExtension(string connectionString, IDbFactory factory)
		{
			ConnectionString = connectionString;
			Factory = factory;
		}

		public bool ApplyServices(IServiceCollection services) => false;
		public long GetServiceProviderHashCode() => 0;
		public void Validate(IDbContextOptions options) { }
	}
}
