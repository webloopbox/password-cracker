namespace backend___central.Services
{
    public class InfoLogService : ILogService
    {

        public void LogMessage(string message)
        {
            string timestamp = ILogService.GetCurrentDate();
            Console.WriteLine($"[INFO] {message} at [{timestamp}]");
        }
    }
}