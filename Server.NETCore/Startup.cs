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

			DbContextOptionsBuilder MainDb(DbContextOptionsBuilder options)
			{
				string mainDb = Configuration.GetValue<string>("MainDb", null);
				if (string.IsNullOrWhiteSpace(mainDb))
				{
					mainDb = UseDefaultDirectory();
					mainDb = $"SQlite;Data Source={Path.Combine(mainDb, $"{serverId}.db")}";
				}

				string[] dbAndConnectionString = ParseDbAndConnectionString(mainDb);

				Log.I($"Main database: '{dbAndConnectionString[0]};{dbAndConnectionString[1]}");

				switch (dbAndConnectionString[0].ToLower())
				{
					case "sqlite":
						Directory.CreateDirectory(Path.GetDirectoryName(dbAndConnectionString[1].Split('=')[1]));
						return options.UseSqlite(dbAndConnectionString[1]);

					case "mssqlserver":
						return options.UseSqlServer(dbAndConnectionString[1]);
				}

				throw new Exception("Incorrect data format in MainDb in appsettings.json.");
			}

			IDb RepositoriesDb(ApplicationDbContext mainDb)
			{
				const string UseMainDb = "UseMainDb";

				string repositoriesDb = Configuration.GetValue<string>("RepositoriesDb", UseMainDb);
				if (string.IsNullOrWhiteSpace(repositoriesDb))
					repositoriesDb = UseMainDb;

				Log.I($"Repositories database: '{repositoriesDb}'");

				switch (repositoriesDb)
				{
					default:
						string[] dbAndConnectionString = ParseDbAndConnectionString(repositoriesDb);
						if (string.IsNullOrWhiteSpace(dbAndConnectionString[1]))
							dbAndConnectionString[1] = UseDefaultDirectory();

						return new DbOnFileSystem(dbAndConnectionString[1]);

					case UseMainDb:
						return new DbOnEF(mainDb.Things, mainDb);
				}
			}

			// Add framework services.

			services.AddDbContext<ApplicationDbContext>(options => MainDb(options));

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

			ApplicationDbContext dbContext = applicationServices.GetService<ApplicationDbContext>();
			dbContext.Database.EnsureCreated();

			services.AddSingleton(
				new Manager()
					.SetId(serverId)
					.UseDb(RepositoriesDb(dbContext))
					.UseSimpleCryptographer(new SimpleCryptographer2DataProtector(protector))
			);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
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

			// Use MVC framework.

			app.UseStaticFiles();

			app.UseIdentity();

			// Add external authentication middleware here. To configure them please see https://go.microsoft.com/fwlink/?LinkID=532715

			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");
			});

			// Initialize pbXStorage.

			await manager.InitializeAsync();
		}
	}
}
