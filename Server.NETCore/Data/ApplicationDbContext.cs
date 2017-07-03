using System;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using pbXNet;

namespace pbXStorage.Server.NETCore.Data
{
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
				if (options.GetExtension<RepositoriesDbOptionsExtension>().Provider == null)
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
