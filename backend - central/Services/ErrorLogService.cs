namespace backend___central.Services
{
    public class ErrorLogService : ILogService
    {
        private string LogContent { get; set; } = "";
        private readonly string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs-backend-central.txt");

        public void LogMessage(string message)
        {
            string timestamp = ILogService.GetCurrentDate();
            LogContent = $"[ERROR] {message} at [{timestamp}]";
            Console.WriteLine(LogContent);
            SaveToFile();
        }

        public void SaveToFile()
        {
            try
            {
                File.AppendAllText(logFilePath, LogContent + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to write [ERROR] log to file: {ex.Message}");
            }
        }
    }
}