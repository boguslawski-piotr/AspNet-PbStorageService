using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using pbXNet;

namespace pbXStorage.Server
{
	class ILogger2MicrosoftILogger : pbXNet.ILogger
	{
		Microsoft.Extensions.Logging.ILogger _logger;

		public ILogger2MicrosoftILogger(Microsoft.Extensions.Logging.ILogger logger)
		{
			_logger = logger;
		}

		public void L(DateTime dt, LogType type, string msg)
		{
			msg = dt.ToString("yyyy-M-d H:m:s.fff") + ": " + $"{msg}";
			switch (type)
			{
				case LogType.Debug:
					_logger.LogDebug(msg);
					break;
				case LogType.Info:
					_logger.LogInformation(msg);
					break;
				case LogType.Warning:
					_logger.LogWarning(msg);
					break;
				case LogType.Error:
					_logger.LogError(msg);
					break;
			}
		}
	}

	class SimpleCryptographer2DataProtector : ISimpleCryptographer
	{
		IDataProtector _protector;

		public SimpleCryptographer2DataProtector(IDataProtector protector)
		{
			_protector = protector;
		}

		public string Encrypt(string data) => _protector.Protect(data);
		public string Decrypt(string data) => _protector.Unprotect(data);
	}

	public class Startup
	{
		public IConfigurationRoot Configuration { get; }

		public Startup(IHostingEnvironment env)
		{
			var builder = new ConfigurationBuilder()
				.SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				.AddEnvironmentVariables();

			Configuration = builder.Build();
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			// Add framework services.

			services.AddMvc();
			services.AddDataProtection();

			// Add pbXStorage manager.

			IServiceProvider applicationServices = services.BuildServiceProvider();
			IDataProtector protector = applicationServices.GetDataProtector(new string[] { "pbXStorage", "Server", "v1" });

			services.AddSingleton(
				new Manager()
					.SetId(Configuration.GetValue<string>("pbXStorageId", null))
					.UseSimpleCryptographer(new SimpleCryptographer2DataProtector(protector))
					.UseNewtonsoftJSonSerializer()
					.UseDb(new DbOnFileSystem())
			);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public async void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, Manager manager)
		{
			// Create ASP.NET standard loggers.

			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();

			// Create bridge from pbXNet logging system to ASP.NET logging system.

			Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger("pbXStorage.Server");
			Log.AddLogger(new ILogger2MicrosoftILogger(logger));

			// Setup error page.

			if (env.IsDevelopment())
				app.UseDeveloperExceptionPage();
			else
				app.UseExceptionHandler("/Home/Error");

			// Use MVC framework.

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
