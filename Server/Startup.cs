using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using pbXNet;

namespace pbXStorage.Server
{
	public class pbXNetILogger2MicrosoftExtensionsLoggingILogger : pbXNet.ILogger
	{
		Microsoft.Extensions.Logging.ILogger _logger;

		public pbXNetILogger2MicrosoftExtensionsLoggingILogger(Microsoft.Extensions.Logging.ILogger logger)
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
        public async void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();

			// Add pbXStorage manager.
			Manager manager = new Manager();
			services.AddSingleton<Manager>(manager);

			// Configure/initialize pbXStorage manager.
			await manager.InitializeAsync();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
			// Create ASP.NET standard loggers.
			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

			// Create bridge from pbXNet logging system to ASP.NET logging system.
			Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger("pbXStorage.Server");
			Log.AddLogger(new pbXNetILogger2MicrosoftExtensionsLoggingILogger(logger));

			// Use MVC framework.
			app.UseMvc();

			//app.UseWelcomePage(); // TODO: zrobic swoja...
        }
    }
}
