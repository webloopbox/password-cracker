using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using backend___central.Interfaces;
using backend___central.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace backend___central
{
    public class Startup
    {

        private IEnumerable<ILogService>? logServices;
        public static bool IsDatabaseRunning { get; private set; } = false;
        public static List<IPAddress> ServersIpAddresses { get; set; } = new List<IPAddress>();
        public static int DictionaryGranularity { get; set; } = 10000;
        public static int BruteForceGranularity { get; set; } = 10;
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration) 
        {
            Env.Load("../.env");
            Configuration = configuration;
        }

        public async void Configure(IApplicationBuilder app, IEnumerable<ILogService> logServices)
        {
            this.logServices = logServices;
            ConfigureApp(app);
            await Task.Run(() => TestConnectionWithDatabase());
        }

        public void ConfigureServices(IServiceCollection services)
        {
            if (services != null)
            {
                services.AddControllers();
                services.AddScoped<DictionarySynchronizingService>();
                services.AddScoped<ILogService, InfoLogService>();
                services.AddScoped<ILogService, ErrorLogService>();
                services.AddScoped<ICrackingService, CrackingService>();
                services.AddScoped<IDictionarySynchronizingService, DictionarySynchronizingService>();
                services.AddScoped<ICheckService, CheckService>();
                services.AddScoped<CheckService>();
                services.AddScoped<IResponseProcessingService, ResponseProcessingService>();
                services.AddScoped<IServerCommunicationService, ServerCommunicationService>();
                services.AddScoped<IBruteForceCrackingService, BruteForceCrackingService>();
                services.AddScoped<IDictionaryCrackingService, DictionaryCrackingService>();
                services.AddScoped<ICrackingService, CrackingService>();
                services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
                {
                    options.MultipartBodyLengthLimit = 32212254720;
                });
                ConfigureCors(services);
            }
        }

        private void ConfigureApp(IApplicationBuilder app)
        {
            app.UseCors("AllowedOrigins");
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
            LogCentralServerInfo("Central web server started");
        }

        private static void ConfigureCors(IServiceCollection services)
        {
            services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 32212254720;
            });
            services.AddCors(options =>
            {
                options.AddPolicy("AllowedOrigins", policy =>
                    policy.SetIsOriginAllowed(origin =>
                    {
                        Uri uri = new(origin);
                        return uri.Port == 5099 || uri.Port == 5173;
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                );
            });
        }

        private void LogCentralServerInfo(string message)
        {
            ILogService? infoLogService = logServices?.FirstOrDefault(logService => logService is InfoLogService);
            infoLogService?.LogMessage(message);
        }

        private void LogCentralServerError(string message)
        {
            ILogService? errorLogService = logServices?.FirstOrDefault(logService => logService is ErrorLogService);
            errorLogService?.LogMessage($"An error occurred: {message}");
        }

        private void TestConnectionWithDatabase()
        {
            try
            {
                string? filePath = Environment.GetEnvironmentVariable("PASSWORD_FILE_PATH");
                bool isFilePathValid = !string.IsNullOrEmpty(filePath);
                if (isFilePathValid)
                {
                    if (File.Exists(filePath))
                    {
                        LogCentralServerInfo($"Successfully found password file at {filePath}");
                        IsDatabaseRunning = true;
                        return;
                    }
                    throw new FileNotFoundException($"Password file not found: {filePath}");
                }
                throw new Exception("Password file path is null or empty");
            }
            catch (Exception ex)
            {
                IsDatabaseRunning = false;
                LogCentralServerError($"File connection test exception: {ex.Message}");
                LogCentralServerError($"Stack Trace: {ex.StackTrace}");
            }
        }
    }
}