using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SignalRMySQLQueue
{
    public class Startup
    {
        readonly string MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(name: MyAllowSpecificOrigins,
                                  builder =>
                                  {
                                      builder.WithOrigins(
                                            null,
                                            "*",
                                            "https://web.postman.co/",
                                            "https://localhost:44306/");
                                  });
            });
            services.AddSignalR();

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            MySQL.DbConnection.ConnString = ConfigurationExtensions.GetConnectionString(Configuration, "DefaultConnectionString");
            MySQL.DbConnection.CacheConnString = ConfigurationExtensions.GetConnectionString(Configuration, "CacheConnectionString");
            Hubs.DbQueueConfig.ThreadCount = Convert.ToInt32(ConfigurationExtensions.GetConnectionString(Configuration, "DbQueueThreadCount"));
            Hubs.DbQueueConfig.MaxConnections = Convert.ToInt32(ConfigurationExtensions.GetConnectionString(Configuration, "DbQueueMaxConnections"));
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors(builder =>
            {
                builder.AllowAnyHeader();
                builder.AllowAnyMethod();
                builder.SetIsOriginAllowed(origin => true);
                builder.AllowCredentials();
            });

            app.UseSignalR(routes =>
            {
                routes.MapHub<Hubs.DbQueue>("/dbqueue");
            });
        }
    }
}
