
namespace backend___central.Services
{
    public class CalculatingServerService(IEnumerable<ILogService> logServices) : ICalculatingServerService
    {
        private readonly IEnumerable<ILogService> logServices = logServices;

        public IResult HandleConnectToCentralServerRequest(HttpContext httpContext)
        {
            try
            {
                string? ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                ILogService.LogInfo(logServices, $"Made request to try to connect calculating server from IP address: " + ipAddress);
                HandleCheckIfDatabaseIsAlive();
                return Results.Ok();
            }
            catch (Exception ex)
            {
                ILogService.LogError(logServices, $"Cannot connect to calculating server due to: {ex.Message}");
                return Results.Problem($"An error occurred while trying to connect calculating server: {ex.Message}");
            }
        }

        private static void HandleCheckIfDatabaseIsAlive()
        {
            if (Program.IsDatabaseRunning == false)
            {
                throw new Exception("database for calculating operations is not running");
            }
        }
    }
}