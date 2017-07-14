using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using pbXNet;
using pbXStorage.Repositories;

namespace pbXStorage.Server.AspNetCore.Data
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

					// I'm not able to force this line to work, regardless of where I place the assembly file :(
					// TODO: try harder...
					//Assembly providerAssembly = Assembly.Load(providerAssemblyName);

					// This works OK, but, according to Microsoft, Assembly.LoadFrom is obsolete :(
					Assembly thisAssembly = typeof(DbContextOptionsBuilderExtensions).Assembly;
					Assembly providerAssembly = Assembly.LoadFrom(
						Path.Combine(Path.GetDirectoryName(thisAssembly.Location), providerAssemblyName.Name) + ".dll"
					);

					Log.I($"loaded assembly '{providerAssembly.FullName}'.");

					string dbFactoryClassName = providerNames[0].Trim();
					var dbFactoryInstance = (IDbFactory)providerAssembly.CreateInstance(dbFactoryClassName, false);

					Log.I($"class '{dbFactoryInstance.GetType()}' will be used to create database connection objects.");

					return builder
						.UseDbFactory(connectionString, dbFactoryInstance);
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
