using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pbXNet;

namespace pbXStorage.Server.NETCore.Data
{
	public static class DbContextOptionsBuilderExtensions
	{
		static Provider ProviderFromName(string providerName)
		{
			switch (providerName.ToLower())
			{
				case "sqlite":
					return Provider.SQlite;
				case "sqlserver":
					return Provider.SqlServer;
				case "dbonfilesystem":
					return Provider.DbOnFileSystem;
				default:
					throw new Exception($"Unsupported database provider '{providerName}'.");
			}
		}

		public static DbContextOptionsBuilder UseDb(this DbContextOptionsBuilder builder, string providerName, string connectionString, string dbName, Provider[] unsupportedProviders = null)
		{
			if (string.IsNullOrWhiteSpace(providerName))
				throw new ArgumentNullException(nameof(providerName));

			Provider provider = ProviderFromName(providerName);
			if (unsupportedProviders != null)
			{
				if(unsupportedProviders.Contains(provider))
					throw new Exception($"Unsupported database provider '{providerName}' for '{dbName}'.");
			}

			switch (provider)
			{
				case Provider.SQlite:
					string dbDirectory = Path.GetDirectoryName(connectionString.Split('=')[1]);
					if (!string.IsNullOrWhiteSpace(dbDirectory))
						Directory.CreateDirectory(dbDirectory);
					builder.UseSqlite(connectionString);
					break;

				case Provider.SqlServer:
					builder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure());
					break;

				case Provider.DbOnFileSystem:
					((IDbContextOptionsBuilderInfrastructure)builder)
						.AddOrUpdateExtension(new DbOnFileSystemOptionsExtension(connectionString));
					break;
			}

			Log.I($"'{providerName};{connectionString}' for '{dbName}'.");

			return builder;
		}
	}
}
