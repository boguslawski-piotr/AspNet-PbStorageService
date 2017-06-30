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
		const string _dbDirectory = ".pbXStorage";

		public IConfigurationRoot Configuration { get; }

		public Startup(IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
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
			string serverId = Configuration.GetValue<string>("ServerId");
			string dataDirectory = Configuration.GetValue<string>("DataDirectory", Directory.GetCurrentDirectory());
			dataDirectory = Path.Combine(dataDirectory, serverId);
			string dbConnection = $"Data Source={Path.Combine(dataDirectory, $"{serverId}.db")}";

			// Add framework services.

			services.AddDbContext<ApplicationDbContext>(options =>
                options
					.UseSqlite(dbConnection));

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
				new Manager()
					.SetId(serverId)
					.UseDb(new DbOnFileSystem(dataDirectory))
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
