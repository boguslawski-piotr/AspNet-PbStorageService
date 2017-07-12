using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace pbXStorage.Server.NETCore.Data
{
	class DbOnFileSystemOptionsExtension : IDbContextOptionsExtension
	{
		public string Directory;

		public DbOnFileSystemOptionsExtension(string directory)
		{
			Directory = directory;
		}

		public bool ApplyServices(IServiceCollection services) => false;
		public long GetServiceProviderHashCode() => 0;
		public void Validate(IDbContextOptions options) { }
	}
}
