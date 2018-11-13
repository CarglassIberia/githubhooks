using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.HealthChecks;
using Octopus.Client;
using Polly;
using Polly.Extensions.Http;

namespace githubhooks
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddGitHubWebHooks();

            services.AddHealthChecks(checks =>
            {
                checks.AddUrlCheck(Configuration["OctoDeployServer:Url"]);
                checks.AddUrlCheck("https://www.github.com/");
            });

            services.AddScoped<OctopusServerEndpoint>(srv => new OctopusServerEndpoint(Configuration["OctoDeployServer:Url"], Configuration["OctoDeployServer:Key"]));

            var policy = HttpPolicyExtensions.HandleTransientHttpError()
                             .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
                             .WaitAndRetryAsync(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            services.AddHttpClient("GitHub", client =>
            {
                client.BaseAddress = new Uri("https://api.github.com/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "Carglass Hooks");
            }).AddPolicyHandler(policy)
              .ConfigureHttpMessageHandlerBuilder(c =>
              {
                  new HttpClientHandler().ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
                  {
                      return errors == SslPolicyErrors.None;
                  };
              });


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseMvc();
        }
    }
}
