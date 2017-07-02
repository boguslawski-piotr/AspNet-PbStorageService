using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
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
		public IConfigurationRoot Configuration { get; }

		IHostingEnvironment _hosttingEnvironment;

		public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			_hosttingEnvironment = env;

			var builder = new ConfigurationBuilder()
				.SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

			//if (env.IsDevelopment())
			//{
			//    // For more details on using the user secret store see https://go.microsoft.com/fwlink/?LinkID=532709
			//    builder.AddUserSecrets<Startup>();
			//}

			builder.AddEnvironmentVariables();
			Configuration = builder.Build();

			// Create ASP.NET standard loggers.

			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();

			// Create bridge from pbXNet logging system to ASP.NET logging system.

			Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger("pbXStorage.Server");
			Log.AddLogger(new ILogger2MicrosoftILogger(logger));
		}

		public void ConfigureServices(IServiceCollection services)
		{
			// Setup.

			string serverId = Configuration.GetValue<string>("ServerId");

			DbContextOptionsBuilder ConfigureDbs(DbContextOptionsBuilder builder)
			{
				(string, string) ParseProviderAndConnectionString(string entryName, string defaultValue)
				{
					string v = Configuration.GetValue<string>(entryName, null);
					if (string.IsNullOrWhiteSpace(v))
						v = defaultValue;
					if (string.IsNullOrWhiteSpace(v))
						return (null, null);

					string[] pcs =
						Environment.ExpandEnvironmentVariables(v)
						.Replace("%ServerId%", serverId)
						.Replace("%ContentRootPath%", _hosttingEnvironment.ContentRootPath)
						.Replace('/', Path.DirectorySeparatorChar)
						.Split(new char[] { ';' }, 2);

					return (pcs[0], pcs.Length > 1 ? pcs[1] : "");
				}

				(string provider, string connectionString) = ParseProviderAndConnectionString("MainDb", $"SQlite;Data Source={serverId}.db");
				builder.UseDb(provider, connectionString);

				(provider, connectionString) = ParseProviderAndConnectionString("RepositoriesDb", null);
				builder.UseRepositoriesDb(provider, connectionString);

				return builder;
			}

			// Add framework services.

			services.AddDbContext<ApplicationDbContext>(
				(builder) => ConfigureDbs(builder)
			);

			services.AddIdentity<ApplicationUser, IdentityRole>()
				.AddEntityFrameworkStores<ApplicationDbContext>()
				.AddDefaultTokenProviders();

			services.AddMvc();

			services.AddDataProtection();

			// Add other services.

			services.AddTransient<IEmailSender, AuthMessageSender>();
			services.AddTransient<ISmsSender, AuthMessageSender>();

			// Add pbXStorage manager.

			IServiceProvider applicationServices = services.BuildServiceProvider();

			IDataProtector protector = applicationServices.GetDataProtector(serverId);

			services.AddSingleton(
				new Manager(serverId, new SimpleCryptographer2DataProtector(protector))
			);
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

			app.UseIdentity();

			// Add external authentication middleware here. To configure them please see https://go.microsoft.com/fwlink/?LinkID=532715

			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");
			});

			// Initialize main db.

			ApplicationDbContext dbContext = app.ApplicationServices.GetService<ApplicationDbContext>();
			dbContext.Database.EnsureCreated();

			// Initialize pbXStorage.

			await manager.InitializeAsync(
				manager.CreateContext(
					dbContext.RepositoriesDb
				)
			);
		}
	}
}
