using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using pbXNet;
using pbXStorage.Server.NETCore.Data;
using pbXStorage.Server.NETCore.Models;
using pbXStorage.Server.NETCore.Services;

namespace pbXStorage.Server.NETCore
{
	public class Startup
	{
		IHostingEnvironment HosttingEnvironment { get; }

		IConfiguration Configuration { get; }

		public Startup(IHostingEnvironment env, IConfiguration configuration)
		{
			HosttingEnvironment = env;
			Configuration = configuration;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			// Setup.

			string serverId = Configuration.GetValue<string>("ServerId");

			DbContextOptionsBuilder ConfigureDbs(DbContextOptionsBuilder builder, string dbName, Provider[] unsupportedProviders = null)
			{
				(string, string) ParseProviderAndConnectionString(string defaultValue)
				{
					string v = Configuration.GetValue<string>(dbName, null);
					if (string.IsNullOrWhiteSpace(v))
						v = defaultValue;
					if (string.IsNullOrWhiteSpace(v))
						return (null, null);

					string[] pcs =
						Environment.ExpandEnvironmentVariables(v)
						.Replace("%ServerId%", serverId)
						.Replace("%ContentRootPath%", HosttingEnvironment.ContentRootPath)
						.Replace('/', Path.DirectorySeparatorChar)
						.Split(new char[] { ';' }, 2);

					return (pcs[0], pcs.Length > 1 ? pcs[1] : "");
				}

				(string provider, string connectionString) = ParseProviderAndConnectionString($"SQlite;Data Source={serverId}-{dbName}.db");

				return builder
					.UseDb(provider, connectionString, dbName, unsupportedProviders);
			}

			// Add databases.

			services.AddDbContext<UsersDb>(
				builder => ConfigureDbs(builder, "UsersDb", new Provider[] { Provider.DbOnFileSystem })
			);

			services.AddDbContext<RepositoriesDb>(
				builder => ConfigureDbs(builder, "RepositoriesDb")
			);

			// Add framework services.

			services.AddIdentity<ApplicationUser, IdentityRole>(config =>
				{
					//config.SignIn.RequireConfirmedEmail = true;
				})
				.AddEntityFrameworkStores<UsersDb>()
				.AddDefaultTokenProviders();

			services.AddMvc();

			services.AddDataProtection();

			// Add other services.

			services.AddTransient<IEmailSender, AuthMessageSender>();

			services.AddTransient<ISmsSender, AuthMessageSender>();

			//services.Configure<AuthMessageSenderOptions>(Configuration);

			services.AddSingleton<ISerializer>(new NewtonsoftJsonSerializer());

			services.AddSingleton(new ContextBuilder(serverId));

			// Add pbXStorage manager.

			services.AddSingleton(new Manager(serverId, TimeSpan.FromHours(Configuration.GetValue<int>("ObjectsLifeTime", 12))));
		}

		public async void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, Manager manager)
		{
			// Setup error page.

			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseDatabaseErrorPage();
				app.UseBrowserLink();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
			}

			// Use/initialize MVC framework.

			app.UseStaticFiles();

			app.UseAuthentication(); 

			// Add external authentication middleware here. To configure them please see https://go.microsoft.com/fwlink/?LinkID=532715

			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");
			});

			// Initialize dbs.

			UsersDb usersDb = app.ApplicationServices.GetService<UsersDb>();
			usersDb.Create();

			RepositoriesDb repositoriesDb = app.ApplicationServices.GetService<RepositoriesDb>();
			await repositoriesDb.CreateAsync();
		}
	}
}
