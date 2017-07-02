using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pbXNet;

namespace pbXStorage.Server.NETCore.Data
{
	class RepositoriesDbOptions : IDbContextOptionsExtension
	{
		public const string SQliteProvider = "sqlite";
		public const string SqlServerProvider = "sqlserver";
		public const string DbOnFileSystemProvider = "dbonfilesystem";

		public string Provider;
		public string ConnectionString;

		public RepositoriesDbOptions(string provider, string connectionString)
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

	public static class DbContextOptionsBuilderExtensions
	{
		public static DbContextOptionsBuilder UseDb(this DbContextOptionsBuilder builder, string provider, string connectionString)
		{
			Log.I($"'{provider};{connectionString}'");

			switch (provider.ToLower())
			{
				case RepositoriesDbOptions.SQliteProvider:
					string dbDirectory = Path.GetDirectoryName(connectionString.Split('=')[1]);
					if (!string.IsNullOrWhiteSpace(dbDirectory))
						Directory.CreateDirectory(dbDirectory);

					builder.UseSqlite(connectionString);
					break;

				case RepositoriesDbOptions.SqlServerProvider:
					builder.UseSqlServer(connectionString);
					break;

				default:
					throw new Exception("Unsupported provider in MainDb configuration entry.");
			}

			return builder;
		}

		public static DbContextOptionsBuilder UseRepositoriesDb(this DbContextOptionsBuilder builder, string provider, string connectionString)
		{
			if (provider != null)
			{
				switch (provider.ToLower())
				{
					case RepositoriesDbOptions.DbOnFileSystemProvider:
						break;
					default:
						throw new Exception("Unsupported provider in RepositoriesDb configuration entry.");
				}

				Log.I($"'{provider};{connectionString}'");
			}
			else
				Log.I($"same as in UseDb");


			((IDbContextOptionsBuilderInfrastructure)builder)
				.AddOrUpdateExtension(
					new RepositoriesDbOptions(provider, connectionString)
				);

			return builder;
		}
	}
}
