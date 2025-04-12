using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using backend___calculating.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DotNetEnv;

namespace backend___calculating
{
    public class CalculatingStartup
    {

        private IApplicationBuilder? app;
        private IEnumerable<ILogService>? logServices;
        public static bool IsDatabaseRunning { get; private set; } = false;
        public static List<IPAddress> ServersIpAddresses { get; set; } = new List<IPAddress>();

        public IConfiguration Configuration { get; }

        public CalculatingStartup(IConfiguration configuration)
        {
            Env.Load(".env");
            Configuration = configuration;
        }

        public async void Configure(IApplicationBuilder app, IEnumerable<ILogService> logServices)
        {
            this.app = app;
            this.logServices = logServices;
            ConfigureApp(app);
            await Task.Run(() => TestConnectionWithDatabase());
        }

        public void ConfigureServices(IServiceCollection services)
        {
            if (services != null)
            {
                ConfigureCors(services);
                services.AddScoped<CheckService>();
                services.AddScoped<DictionaryService>();
                services.AddScoped<ICheckService, CheckService>();
                services.AddScoped<IBruteForceService, BruteForceService>();
                services.AddScoped<IDictionaryService, DictionaryService>();
                services.AddScoped<ILogService, InfoLogService>();
                services.AddScoped<ILogService, ErrorLogService>();
                services.AddControllers();
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
            LogCalculatingServerInfo("Calculating web server started");
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
                        return uri.Port == 5098;
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                );
            });
        }

        private void LogCalculatingServerInfo(string message)
        {
            ILogService? infoLogService = logServices?.FirstOrDefault(logService => logService is InfoLogService);
            infoLogService?.LogMessage(message);
        }

        private void LogCalculatingServerError(string message)
        {
            ILogService? errorLogService = logServices?.FirstOrDefault(logService => logService is ErrorLogService);
            errorLogService?.LogMessage($"An error occurred: {message}");
        }

        private async Task TestConnectionWithDatabase()
        {
            try
            {
                string? connection = Env.GetString("POSTGRES_DB_CONNECTION_STRING");
                bool isConnectionStringValid = !string.IsNullOrEmpty(connection);
                if (isConnectionStringValid)
                {
                    using Npgsql.NpgsqlConnection connectionReference = new(connection);
                    await connectionReference.OpenAsync();
                    LogCalculatingServerInfo("Successfully connected to the PostgreSQL database instance");
                    IsDatabaseRunning = true;
                    await ConnectWithCentralServer();
                    return;
                }
                throw new Exception("Database Connection string is null or empty");
            }
            catch (Exception ex)
            {
                IsDatabaseRunning = false;
                LogCalculatingServerError($"Database test connection exception {ex.Message}");
                StopCalculatingServer();
            }
        }

        private async Task ConnectWithCentralServer()
        {
            try
            {
                Env.Load(".env");
                string? centralServerIp = Env.GetString("CENTRAL_SERVER_IP");
                string? calculatingServerIp = Env.GetString("CALCULATING_SERVER_IP");
                if (string.IsNullOrEmpty(centralServerIp))
                {
                    throw new Exception("CENTRAL_SERVER_IP is not set in the .env file");
                }
                if (string.IsNullOrEmpty(calculatingServerIp))
                {
                    throw new Exception("CALCULATING_SERVER_IP is not set in the .env file");
                }
                using HttpClient httpClient = new();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                string centralServerUri = $"http://{centralServerIp}:5098/api/calculating-server/connect";
                using MultipartFormDataContent formData = new()
                {
                    { new StringContent(calculatingServerIp), "IpAddress" }
                };
                HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(centralServerUri, formData);
                if (!httpResponseMessage.IsSuccessStatusCode)
                {
                    throw new Exception($"Cannot connect to central server with address {centralServerIp}. Status code: {httpResponseMessage.StatusCode}");
                }
                LogCalculatingServerInfo("Successfully connected to central server");
            }
            catch (Exception ex)
            {
                LogCalculatingServerError($"Error to connect to central server: {ex}");
                StopCalculatingServer();
            }
        }

        private void StopCalculatingServer()
        {
            if (app != null)
            {
                LogCalculatingServerInfo("Stopped calculating server");
                IHostApplicationLifetime lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
                lifetime.StopApplication();
            }
        }
    }
}