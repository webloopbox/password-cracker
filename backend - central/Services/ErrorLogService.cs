namespace backend___central.Services
{
    public class ErrorLogService : ILogService
    {

        public void LogMessage(string message)
        {
            string timestamp = ILogService.GetCurrentDate();
            Console.WriteLine($"[ERROR] {message} at [{timestamp}]");
        }
    }
}