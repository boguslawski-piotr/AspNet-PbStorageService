using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pbXNet;

namespace pbXStorage.Server.NETCore.Data
{
	public static class DbContextOptionsBuilderExtensions
	{
		public static DbContextOptionsBuilder UseDbFactory(this DbContextOptionsBuilder builder, string connectionString, IDbFactory factory)
		{
			((IDbContextOptionsBuilderInfrastructure)builder)
				.AddOrUpdateExtension(new DbFactoryExtension(connectionString, factory));
			return builder;
		}

		public static DbContextOptionsBuilder UseSqliteEx(this DbContextOptionsBuilder builder, string connectionString)
		{
			string dbDirectory = Path.GetDirectoryName(connectionString.Split('=')[1]);
			if (!string.IsNullOrWhiteSpace(dbDirectory))
				Directory.CreateDirectory(dbDirectory);

			return builder.UseSqlite(connectionString);
		}

		public static DbContextOptionsBuilder UseDatabase(this DbContextOptionsBuilder builder, string connectionString, Provider[] unsupportedProviders = null)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentNullException(nameof(connectionString));

			string[] pcs = connectionString.Split(';', 2);
			string providerName = pcs[0].Trim();
			connectionString = (pcs.Length > 1 ? pcs[1] : "").Trim();

			Provider provider = ProviderFromName(providerName, unsupportedProviders);
			switch (provider)
			{
				case Provider.SQlite:
					return builder
						.UseSqliteEx(connectionString)
						.UseDbFactory(connectionString, new SqliteFactory());

				case Provider.SqlServer:
					return builder
						.UseSqlServer(connectionString, options => options.EnableRetryOnFailure())
						.UseDbFactory(connectionString, new SqlServerFactory());

				case Provider.DbOnFileSystem:
					return builder
						.UseDbFactory(connectionString, new DBOnFileSystemFactory());

				default:
					string[] providerNames = providerName.Split(',', 2);

					AssemblyName providerAssemblyName = new AssemblyName(providerNames[1].Trim());
					Assembly providerAssembly = Assembly.Load(providerAssemblyName);
					Log.I($"loaded assembly '{providerAssembly.FullName}'.");

					string dbFactoryClassName = providerNames[0].Trim();
					Type dbFactoryClass = providerAssembly.GetType(dbFactoryClassName, true, true);
					Log.I($"class '{dbFactoryClass}' will be used to create database connection objects.");

					return builder
						.UseDbFactory(connectionString, (IDbFactory)Activator.CreateInstance(dbFactoryClass));
			}
		}

		static Provider ProviderFromName(string providerName, Provider[] unsupportedProviders = null)
		{
			Provider Get()
			{
				switch (providerName.ToLower())
				{
					case "sqlite":
						return Provider.SQlite;
					case "sqlserver":
						return Provider.SqlServer;
					case "dbonfilesystem":
						return Provider.DbOnFileSystem;
				}

				return Provider.External;
			}

			Provider provider = Get();
			if (unsupportedProviders == null || !unsupportedProviders.Contains(provider))
				return provider;

			throw new Exception($"Unsupported database provider '{providerName}'.");
		}
	}
}
