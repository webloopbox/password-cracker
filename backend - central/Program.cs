using DotNetEnv;
using backend___central.Services;

namespace backend___central
{
    public class Program(WebApplicationBuilder webApplicationBuilder)
    {
        private WebApplication? webApplication;
        private IEnumerable<ILogService>? logServices;
        private readonly WebApplicationBuilder webApplicationBuilder = webApplicationBuilder;

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
                DotNetEnv.Env.Load(".env");
                string? connection = DotNetEnv.Env.GetString("POSTGRES_DB_CONNECTION_STRING");
                bool isConnectionStringValid = !string.IsNullOrEmpty(connection);
                if (isConnectionStringValid)
                {
                    using Npgsql.NpgsqlConnection connectionReference = new(connection);
                    await connectionReference.OpenAsync();
                    await LogCentralServerInfo("Successfully connected to the PostgreSQL database instance");
                    return;
                }
                throw new Exception("Database Connection string is null or empty");
            }
            catch (Exception ex)
            {
                LogCentralServerError(ex);
            }
        }

        private async Task RunCentralServer()
        {
            webApplication = webApplicationBuilder.Build();
            _ = webApplication.UseCors("AllowFrontend");
            _ = webApplication.MapControllers();
            await webApplication.RunAsync();
        }

        private void SetupServicesScopes()
        {
            _ = webApplicationBuilder.Services.AddScoped<ILogService, InfoLogService>();
            _ = webApplicationBuilder.Services.AddScoped<ILogService, ErrorLogService>();
            _ = webApplicationBuilder.Services.AddControllers();
            ServiceProvider serviceProvider = webApplicationBuilder.Services.BuildServiceProvider();
            logServices = serviceProvider.GetServices<ILogService>();
        }

        private void SetupCorsPolicy()
        {
            IServiceCollection services = webApplicationBuilder.Services;
            _ = services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()
                );
            });
        }

        public async Task StartAsync()
        {
            try
            {
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

        public static async Task Main(string[] args)
        {
            WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder(args);
            Program program = new(webApplicationBuilder);
            await program.StartAsync();
        }
    }
}
