using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace pbXStorage.Server.NETCore.Data
{
	public class UsersDb : IdentityDbContext<ApplicationUser>
	{
		public UsersDb() 
			: base()
		{ }

		public UsersDb(DbContextOptions<UsersDb> options)
			: base(options)
		{
		}

		public void Create()
		{
			Database.EnsureCreated();
			//Database.Migrate();
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			builder.Entity<ApplicationUserRepository>()
				.HasKey(c => new { c.RepositoryId, c.ApplicationUserId });
			builder.Entity<ApplicationUserRepository>()
				.HasIndex(c => c.ApplicationUserId);
		}
	}
}
