using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Boxed.AspNetCore;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace githubhooks
{
    public class Program
    {
        public static int Main(string[] args) => LogAndRun(CreateWebHostBuilder(args).Build());

        public static int LogAndRun(IWebHost webHost)
        {
            Log.Logger = BuildLogger(webHost);

            try
            {
                Log.Information("Starting application");
                webHost.Run();
                Log.Information("Stopped application");
                return 0;
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "Application terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            new WebHostBuilder()
                .UseConfiguration(new ConfigurationBuilder().AddCommandLine(args).Build())
                .UseKestrel(
                    (builderContext, options) =>
                    {
                        // Do not add the Server HTTP header.
                        options.AddServerHeader = false;
                    })
                .UseHealthChecks("/status", TimeSpan.FromSeconds(3))
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureAppConfiguration((hostingContext, config) =>
                    AddConfiguration(config, hostingContext.HostingEnvironment, args))
                .UseSerilog()
                .UseIISIntegration()
                .UseDefaultServiceProvider((context, options) =>
                    options.ValidateScopes = context.HostingEnvironment.IsDevelopment())
                .UseStartup<Startup>();

        private static IConfigurationBuilder AddConfiguration(
            IConfigurationBuilder configurationBuilder,
            IHostingEnvironment hostingEnvironment,
            string[] args) =>
            configurationBuilder
                // Add configuration from the appsettings.json file.
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                // Add configuration from an optional appsettings.development.json, appsettings.staging.json or
                // appsettings.production.json file, depending on the environment. These settings override the ones in
                // the appsettings.json file.
                .AddJsonFile($"appsettings.{hostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                // This reads the configuration keys from the secret store. This allows you to store connection strings
                // and other sensitive settings, so you don't have to check them into your source control provider.
                // Only use this in Development, it is not intended for Production use. See
                // http://docs.asp.net/en/latest/security/app-secrets.html
                .AddIf(
                    hostingEnvironment.IsDevelopment(),
                    x => x.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true))
                // Add configuration specific to the Development, Staging or Production environments. This config can
                // be stored on the machine being deployed to or if you are using Azure, in the cloud. These settings
                // override the ones in all of the above config files.
                .AddEnvironmentVariables()
                // Add command line options. These take the highest priority.
                .AddIf(
                    args != null,
                    x => x.AddCommandLine(args));

        private static Logger BuildLogger(IWebHost webHost) =>
            new LoggerConfiguration()
                .ReadFrom.Configuration(webHost.Services.GetRequiredService<IConfiguration>())
                .Enrich.WithProperty("Application", GetAssemblyProductName())
                .CreateLogger();

        private static string GetAssemblyProductName() =>
            Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product;
    }

}
