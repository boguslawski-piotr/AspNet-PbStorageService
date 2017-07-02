using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pbXNet;
using pbXStorage.Server.NETCore.Models;
using pbXStorage.Server.NETCore.Services;

namespace pbXStorage.Server.NETCore.Data
{
	class RepositoriesDbOptions : IDbContextOptionsExtension
	{
		public const string MainDbProvider = "MainDb";
		public const string DbOnFileSystemProvider = "DbOnFileSystem";

		public string Provider;
		public string ConnectionString;

		public RepositoriesDbOptions(string provider, string connectionString)
		{
			Provider = provider;
			ConnectionString = connectionString;
		}

		public void ApplyServices(IServiceCollection services)
		{
			if (Provider != RepositoriesDbOptions.MainDbProvider)
				services.AddSingleton(new DbOnFileSystem(ConnectionString));
		}
	}

	public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
	{
		public DbSet<Thing> Things { get; set; }

		public IDb RepositoriesDb => _repositoriesDb ?? this.GetService<DbOnFileSystem>();

		IDb _repositoriesDb;

		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
			: base(options)
		{
			try
			{
				RepositoriesDbOptions _repositoriesDbOptions = options.GetExtension<RepositoriesDbOptions>();
				if (_repositoriesDbOptions.Provider == RepositoriesDbOptions.MainDbProvider)
					_repositoriesDb = new DbOnEF(Things, this);
			}
			catch(Exception ex)
			{
				Log.E(ex.Message, this);
				_repositoriesDb = new DbOnEF(Things, this);
			}
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			builder.Entity<ApplicationUserRepository>()
				.HasKey(c => new { c.RepositoryId, c.ApplicationUserId });
			builder.Entity<ApplicationUserRepository>()
				.HasIndex(c => c.ApplicationUserId);

			builder.Entity<Thing>()
				.HasKey(t => new { t.StorageId, t.Id });
			builder.Entity<Thing>()
				.HasIndex(t => t.StorageId);
			builder.Entity<Thing>()
				.HasIndex(t => t.Id);
			builder.Entity<Thing>()
				.ToTable("Things");
		}
	}
}
