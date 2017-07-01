using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using pbXStorage.Server.NETCore.Models;
using pbXStorage.Server.NETCore.Services;

namespace pbXStorage.Server.NETCore.Data
{
	public class ApplicationUser : IdentityUser
	{
		public List<ApplicationUserRepository> Repositories { get; set; } = new List<ApplicationUserRepository>();
	}

	[Table("AspNetUserRepositories")]
	public class ApplicationUserRepository
	{
		public string RepositoryId { get; set; }
		public string UserId { get; set; }
		public ApplicationUser User { get; set; }
	}

	public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
	{
		public DbSet<Thing> Things { get; set; }

		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
			: base(options)
		{ }

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			builder.Entity<ApplicationUserRepository>()
				.HasKey(c => new { c.RepositoryId, c.UserId });
			builder.Entity<ApplicationUserRepository>()
				.HasIndex(c => c.UserId);

			builder.Entity<Thing>()
				.HasKey(t => new { t.StorageId, t.Id });
			builder.Entity<Thing>()
				.HasIndex(t => t.StorageId);
			builder.Entity<Thing>()
				.HasIndex(t => t.Id);
			builder.Entity<Thing>()
				.ToTable("Things");

			// Customize the ASP.NET Identity model and override the defaults if needed.
			// For example, you can rename the ASP.NET Identity table names and more.
			// Add your customizations after calling base.OnModelCreating(builder);
		}
	}
}
