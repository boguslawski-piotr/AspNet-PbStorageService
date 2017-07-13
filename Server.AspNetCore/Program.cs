using System.IO;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using pbXStorage.Repositories.AspNetCore.Services;

namespace pbXStorage.Repositories.AspNetCore
{
	public class Program
	{
		public static void Main(string[] args)
		{
			string contentRoot = Directory.GetCurrentDirectory();

			var config = new ConfigurationBuilder()
				.SetBasePath(contentRoot)
				.AddJsonFile("hostingsettings.json", optional: true)
				.AddCommandLine(args)
				.Build();

			var host = new WebHostBuilder()
				.UseContentRoot(contentRoot)

				.UseConfiguration(config)

				.UseKestrel(options =>
				{
					IPAddress ParseIPAddress(string s)
					{
						if (string.IsNullOrWhiteSpace(s) || s == "*" || s.ToLower() == "any")
							return IPAddress.Any;
						return IPAddress.Parse(s);
					}

					options.Listen(ParseIPAddress(config["httpAddress"]), config.GetValue<int>("httpPort"));

					string httpsAddress = config["httpsAddress"];
					if (!string.IsNullOrWhiteSpace(httpsAddress))
					{
						options.Listen(ParseIPAddress(httpsAddress), config.GetValue<int>("httpsPort"), listenOptions =>
						{
							// TODO: jak przechowywac/przekazac bezpiecznie certyfikat https?
							//listenOptions.UseHttps("testCert.pfx", "testPassword");
						});
					}
				})

				.UseIISIntegration()

				.ConfigureAppConfiguration((hostingContext, appConfig) =>
				{
					appConfig
						.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
						.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
						.AddEnvironmentVariables();
				})

				.ConfigureLogging((hostingContext, logging) =>
				{
					logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
					logging.AddAzureWebAppDiagnostics();
					logging.AddConsole();
					//logging.AddDebug();

					var logger = logging.Services.BuildServiceProvider().GetService<ILoggerFactory>().CreateLogger("pbXStorage.Repositories");
					pbXNet.Log.AddLogger(new ILogger2MicrosoftILogger(logger));
				})

				.UseStartup<Startup>()

				//.UseApplicationInsights()

				.Build();

			host.Run();
		}
	}
}
