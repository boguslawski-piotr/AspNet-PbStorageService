using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pbXNet;

namespace pbXStorage.Server.NETCore.Data
{
	public static class DbContextOptionsBuilderExtensions
	{
		public static DbContextOptionsBuilder UseDb(this DbContextOptionsBuilder builder, string provider, string connectionString)
		{
			Log.I($"Application database '{provider};{connectionString}'.");

			switch (provider.ToLower())
			{
				case RepositoriesDbOptionsExtension.SQliteProvider:
					string dbDirectory = Path.GetDirectoryName(connectionString.Split('=')[1]);
					if (!string.IsNullOrWhiteSpace(dbDirectory))
						Directory.CreateDirectory(dbDirectory);
					builder.UseSqlite(connectionString);
					break;

				case RepositoriesDbOptionsExtension.SqlServerProvider:
					builder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure());
					break;

				default:
					throw new Exception($"Unsupported database provider '{provider}'.");
			}

			return builder;
		}

		public static DbContextOptionsBuilder UseRepositoriesDb(this DbContextOptionsBuilder builder, string provider, string connectionString)
		{
			if (provider != null)
			{
				switch (provider.ToLower())
				{
					case RepositoriesDbOptionsExtension.DbOnFileSystemProvider:
						break;
					default:
						throw new Exception($"Unsupported repositories database provider '{provider}'.");
				}

				Log.I($"'{provider};{connectionString}'");
			}
			else
				Log.I("data will be stored in application database.");


			((IDbContextOptionsBuilderInfrastructure)builder)
				.AddOrUpdateExtension(
					new RepositoriesDbOptionsExtension(provider, connectionString)
				);

			//return new DbContextOptionsBuilder(builder.Options.WithExtension(new RepositoriesDbOptions(provider, connectionString)));

			return builder;
		}
	}
}
