using DotNetEnv;
using backend___central.Services;
using System.Net;

namespace backend___central
{
    public class Program(WebApplicationBuilder webApplicationBuilder)
    {
        public static List<IPAddress> ServersIpAddresses { get; set; } = [];
        public static bool IsDatabaseRunning { get; private set; } = false;

        private WebApplication? webApplication;
        private IEnumerable<ILogService>? logServices;
        private readonly WebApplicationBuilder webApplicationBuilder = webApplicationBuilder;

        public static async Task Main(string[] args)
        {
            WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder(args);
            Program program = new(webApplicationBuilder);
            await program.StartAsync();
        }

        public async Task StartAsync()
        {
            try
            {
                ConfigureKestrel(webApplicationBuilder);
                ConfigureMultipartBodyLength(webApplicationBuilder); 
                SetupCorsPolicy();
                SetupServicesScopes();
                Task runServerTask = Task.Run(() => RunCentralServer());
                Task logStartTask = Task.Run(() => LogCentralServerInfo("Central web server started"));
                Task checkDbConnectionTask = Task.Run(() => TestConnectionWithDatabase());
                await Task.WhenAll(runServerTask, logStartTask, checkDbConnectionTask);
            }
            catch (Exception ex)
            {
                LogCentralServerError(ex);
                throw;
            }
        }

        private async Task RunCentralServer()
        {
            webApplication = webApplicationBuilder.Build();
            _ = webApplication.UseCors("AllowedOrigins");
            _ = webApplication.MapControllers();
            await webApplication.RunAsync();
        }

        private void SetupServicesScopes()
        {
            _ = webApplicationBuilder.Services.AddScoped<DictionaryService>();
            _ = webApplicationBuilder.Services.AddScoped<ILogService, InfoLogService>();
            _ = webApplicationBuilder.Services.AddScoped<ILogService, ErrorLogService>();
            _ = webApplicationBuilder.Services.AddScoped<ICrackingService, CrackingService>();
            _ = webApplicationBuilder.Services.AddScoped<IDictionaryService, DictionaryService>();
            _ = webApplicationBuilder.Services.AddScoped<ICalculatingServerService, CalculatingServerService>();
            _ = webApplicationBuilder.Services.AddControllers();
            ServiceProvider serviceProvider = webApplicationBuilder.Services.BuildServiceProvider();
            logServices = serviceProvider.GetServices<ILogService>();
        }

        private void SetupCorsPolicy()
        {
            IServiceCollection services = webApplicationBuilder.Services;
            _ = services.AddCors(options =>
            {
                options.AddPolicy("AllowedOrigins", policy =>
                    policy.SetIsOriginAllowed(origin =>
                        {
                            Uri uri = new(origin);
                            return uri.Port == 5173 || uri.Port == 5099;
                        })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                );
            });
        }

        private Task LogCentralServerInfo(string message)
        {
            InfoLogService? infoLogService = logServices?.OfType<InfoLogService>().FirstOrDefault();
            infoLogService?.LogMessage(message);
            return Task.CompletedTask;
        }

        private void LogCentralServerError(Exception ex)
        {
            ErrorLogService? errorLogService = logServices?.OfType<ErrorLogService>().FirstOrDefault();
            errorLogService?.LogMessage($"An error occurred: {ex.Message}");
        }

        private async Task TestConnectionWithDatabase()
        {
            try
            {
                Env.Load(".env");
                string? connection = Env.GetString("POSTGRES_DB_CONNECTION_STRING");
                bool isConnectionStringValid = !string.IsNullOrEmpty(connection);
                if (isConnectionStringValid)
                {
                    using Npgsql.NpgsqlConnection connectionReference = new(connection);
                    await connectionReference.OpenAsync();
                    await LogCentralServerInfo("Successfully connected to the PostgreSQL database instance");
                    IsDatabaseRunning = true;
                    return;
                }
                throw new Exception("Database Connection string is null or empty");
            }
            catch (Exception ex)
            {
                IsDatabaseRunning = false;
                LogCentralServerError(ex);
                if (webApplication != null)
                {
                    await LogCentralServerInfo("Stopped central server");
                    await webApplication.StopAsync();
                }
            }
        }
        private static void ConfigureKestrel(WebApplicationBuilder builder)
        {
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 32212254720;
            });
        }

        private static void ConfigureMultipartBodyLength(WebApplicationBuilder builder)
        {
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 32212254720;
            });
        }
    }
}