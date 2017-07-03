using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace pbXStorage.Server.NETCore
{
	public class Program
    {
		public static void Main(string[] args)
        {
			string contentRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();

			var config = new ConfigurationBuilder()
				.SetBasePath(contentRoot)
				.AddJsonFile("hostingsettings.json", optional: true)
				.Build();

			var host = new WebHostBuilder()
                .UseKestrel()
				.UseContentRoot(contentRoot)
                .UseIISIntegration()
				.UseConfiguration(config)
                .UseStartup<Startup>()
                //.UseApplicationInsights()
                .Build();

            host.Run();
        }
    }
}
