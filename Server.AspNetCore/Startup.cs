using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using pbXNet;
using pbXStorage.Repositories;
using pbXStorage.Server.AspNetCore.Data;
using pbXStorage.Server.AspNetCore.Services;

namespace pbXStorage.Server.AspNetCore
{
	public class Startup
	{
		IHostingEnvironment _hostingEnvironment { get; }

		IConfiguration _configuration { get; }

		Microsoft.Extensions.Logging.ILogger _logger;

		public Startup(IHostingEnvironment hostingEnvironment, IConfiguration configuration)
		{
			_hostingEnvironment = hostingEnvironment;
			_configuration = configuration;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			// Setup.

			_logger = services.BuildServiceProvider().GetService<ILoggerFactory>().CreateLogger("pbXStorage.Startup");

			string serverId = _configuration.GetValue<string>("ServerId");
			if(string.IsNullOrWhiteSpace(serverId))
				throw new Exception("'ServerId' must be defined! Check your 'appsettings.json' file.");

			string ConnectionStringFor(string dbName)
			{
				string v = _configuration.GetValue<string>(dbName, null);
				if (string.IsNullOrWhiteSpace(v))
					v = $"SQlite;Data Source={serverId}-{dbName}.db";

				v =	Environment.ExpandEnvironmentVariables(v)
					.Replace("%ServerId%", serverId)
					.Replace("%ContentRootPath%", _hostingEnvironment.ContentRootPath)
					.Replace('/', Path.DirectorySeparatorChar);

				if(_logger.IsEnabled(LogLevel.Trace))
					_logger.LogTrace($"{dbName}: {v}");
				else
					_logger.LogInformation($"{dbName}: {v?.Split(';')?[0]}");

				return v;
			}

			// Add databases.

			int maxPoolSize = _configuration.GetValue<int>("MaxPoolSize", 128);

			services.AddDbContextPool<UsersDb>(
				builder => builder.UseDatabase(ConnectionStringFor("UsersDb"), new Provider[] { Provider.DbOnFileSystem, Provider.External }),
				maxPoolSize
			);

			services.AddRepositoriesDbPool(
				builder => builder.UseDatabase(ConnectionStringFor("RepositoriesDb")),
				maxPoolSize
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

			services.AddSingleton(new Manager(serverId, TimeSpan.FromHours(_configuration.GetValue<int>("ObjectsLifeTime", 12))));
		}

		public async void Configure(IApplicationBuilder app, IHostingEnvironment env)
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

			RepositoriesDbPool repositoriesDbPool = app.ApplicationServices.GetService<RepositoriesDbPool>();
			IDb repositoriesDb = repositoriesDbPool.Rent();
			await repositoriesDb.CreateAsync();
			repositoriesDbPool.Return(repositoriesDb);
		}
	}
}
