using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;

namespace pbXStorage.Server.NETCore
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
				.UseKestrel()
				.UseIISIntegration()
                .UseStartup<Startup>()
                //.UseApplicationInsights()
                .Build();

            host.Run();
        }
    }
}
