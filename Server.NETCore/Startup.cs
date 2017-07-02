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

		IHostingEnvironment _env;

		public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			_env = env;

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

			DbContextOptionsBuilder ConfigureDbs(DbContextOptionsBuilder options)
			{
				string[] ParseDbAndConnectionString(string s)
				{
					return
						Environment.ExpandEnvironmentVariables(s)
						.Replace("%ServerId%", serverId)
						.Replace('/', Path.DirectorySeparatorChar)
						.Split(new char[] { ';' }, 2);
				}

				string UseDefaultDirectory()
				{
					string dir = Path.Combine(_env.ContentRootPath, serverId);
					Directory.CreateDirectory(dir);
					return dir;
				}

				string mainDb = Configuration.GetValue<string>(RepositoriesDbOptions.MainDbProvider, null);
				if (string.IsNullOrWhiteSpace(mainDb))
					mainDb = $"SQlite;Data Source={Path.Combine(UseDefaultDirectory(), $"{serverId}.db")}";

				string[] dbAndConnectionString = ParseDbAndConnectionString(mainDb);
				string provider = dbAndConnectionString[0];
				string connectionString = dbAndConnectionString.Length > 1 ? dbAndConnectionString[1] : "";

				Log.I($"Main database: '{provider};{connectionString}'");

				switch (provider.ToLower())
				{
					case "sqlite":
						Directory.CreateDirectory(Path.GetDirectoryName(connectionString.Split('=')[1]));
						options.UseSqlite(connectionString);
						break;

					case "mssqlserver":
						options.UseSqlServer(connectionString);
						break;

					default:
						throw new Exception("Incorrect format in MainDb entry in appsettings.json.");
				}

				string repositoriesDb = Configuration.GetValue<string>("RepositoriesDb", null);
				if (string.IsNullOrWhiteSpace(repositoriesDb))
					repositoriesDb = RepositoriesDbOptions.MainDbProvider;

				dbAndConnectionString = ParseDbAndConnectionString(repositoriesDb);
				provider = dbAndConnectionString[0];
				connectionString = dbAndConnectionString.Length > 1 ? dbAndConnectionString[1] : "";

				if (provider != RepositoriesDbOptions.MainDbProvider)
				{
					if (string.IsNullOrWhiteSpace(connectionString))
						connectionString = UseDefaultDirectory();
					Directory.CreateDirectory(connectionString);
				}

				Log.I($"Repositories database: '{provider};{connectionString}'");

				((IDbContextOptionsBuilderInfrastructure)options)
					.AddOrUpdateExtension(
						new RepositoriesDbOptions(provider, connectionString)
					);
				return options;
			}

			// Add framework services.

			services.AddDbContext<ApplicationDbContext>(
				(options) => ConfigureDbs(options)
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
