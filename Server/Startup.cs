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
	public class PbXNetILogger2MicrosoftExtensionsLoggingILogger : pbXNet.ILogger
	{
		Microsoft.Extensions.Logging.ILogger _logger;

		public PbXNetILogger2MicrosoftExtensionsLoggingILogger(Microsoft.Extensions.Logging.ILogger logger)
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

			//DataProtectionOptions a = new DataProtectionOptions();
			services.AddDataProtection();

			// Add pbXStorage manager.
			services.AddSingleton(new Manager());
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public async void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			// Create ASP.NET standard loggers.

			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();

			// Create bridge from pbXNet logging system to ASP.NET logging system.

			Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger("pbXStorage.Server");
			Log.AddLogger(new PbXNetILogger2MicrosoftExtensionsLoggingILogger(logger));

			// Use MVC framework.

			app.UseMvc();

			// Configure/initialize pbXStorage.

			IDataProtector protector = app.ApplicationServices.GetDataProtector(new string[] { "pbXStorage", "Server", "v1" });

			Manager manager = app.ApplicationServices.GetService<Manager>();

			manager.Serializer = new NewtonsoftJsonSerializer();
			manager.Decrypter = protector.Unprotect;
			manager.Encrypter = protector.Protect;

			await manager.UseDbAsync<DbOnFileSystem>();

			await manager.InitializeAsync();
		}
	}
}
