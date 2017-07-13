using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace pbXStorage.Repositories.AspNetCore.Data
{
	public static class ServiceCollectionExtensions
	{
		public static IServiceCollection AddRepositoriesDbPool(this IServiceCollection serviceCollection, Action<DbContextOptionsBuilder> optionsAction, int poolSize = 128)
		{
			DbContextOptionsBuilder builder = new DbContextOptionsBuilder();

			optionsAction?.Invoke(builder);

			serviceCollection.AddSingleton(
				new RepositoriesDbPool(
					builder.Options
						.WithExtension(new CoreOptionsExtension().WithMaxPoolSize(poolSize))
				)
			);

			return serviceCollection;
		}
	}
}
