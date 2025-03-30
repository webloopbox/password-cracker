using DotNetEnv;
using backend___calculating.Services;

namespace backend___calculating
{
    public class Program(WebApplicationBuilder webApplicationBuilder)
    {

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
                Task runServerTask = Task.Run(() => RunCalculatingServer());
                Task logStartTask = Task.Run(() => LogCalculatingServerInfo("Calculating web server started"));
                Task checkDbConnectionTask = Task.Run(() => TestConnectionWithDatabase());
                await Task.WhenAll(runServerTask, logStartTask, checkDbConnectionTask);
            }
            catch (Exception ex)
            {
                LogCalculatingServerError(ex);
                throw;
            }
        }

        private async Task RunCalculatingServer()
        {
            webApplication = webApplicationBuilder.Build();
            _ = webApplication.UseCors("AllowedOrigins");
            _ = webApplication.MapControllers();
            await webApplication.RunAsync();
        }

        private void SetupServicesScopes()
        {
            _ = webApplicationBuilder.Services.AddScoped<ILogService, InfoLogService>();
            _ = webApplicationBuilder.Services.AddScoped<ILogService, ErrorLogService>();
            _ = webApplicationBuilder.Services.AddScoped<IDictionaryService, DictionaryService>();
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
                            return uri.Port == 5098;
                        })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                );
            });
        }

        private Task LogCalculatingServerInfo(string message)
        {
            InfoLogService? infoLogService = logServices?.OfType<InfoLogService>().FirstOrDefault();
            infoLogService?.LogMessage(message);
            return Task.CompletedTask;
        }

        private void LogCalculatingServerError(Exception ex)
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
                    await LogCalculatingServerInfo("Successfully connected to the PostgreSQL database instance");
                    await ConnectWithCentralServer();
                    return;
                }
                throw new Exception("Database Connection string is null or empty");
            }
            catch (Exception ex)
            {
                LogCalculatingServerError(ex);
                await StopCalculatingServer();
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
                await LogCalculatingServerInfo("Successfully connected to central server");
            }
            catch (Exception ex)
            {
                LogCalculatingServerError(ex);
                await StopCalculatingServer();
            }
        }

        private async Task StopCalculatingServer()
        {
            if (webApplication != null)
            {
                await LogCalculatingServerInfo("Stopped calculating server");
                await webApplication.StopAsync();
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