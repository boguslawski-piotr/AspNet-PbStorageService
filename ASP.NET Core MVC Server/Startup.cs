using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace pbXStorage.AspNetCore.MVC
{
	public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();

			// Adds a default in-memory implementation of IDistributedCache.
			services.AddDistributedMemoryCache();

			services.AddSession(options =>
			{
				options.CookieSecure = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
				options.CookieHttpOnly = true;
				options.CookieName = "pbXStorage";

				// Set a short timeout for easy testing.
				// TODO: remember about cange to real value after testing
				options.IdleTimeout = TimeSpan.FromSeconds(10);
			});
		}

		// Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

			app.UseSession();
            app.UseMvc();
        }
    }
}
