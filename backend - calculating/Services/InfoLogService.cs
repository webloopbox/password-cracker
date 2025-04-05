using System;
using System.IO;

namespace backend___calculating.Services
{
    public class InfoLogService : ILogService
    {
        private string LogContent { get; set; } = "";
        private readonly string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs-backend-calculating.txt");

        public override void LogMessage(string message)
        {
            string timestamp = GetCurrentDate();
            LogContent = $"[INFO] {message} at [{timestamp}]";
            Console.WriteLine(LogContent);
            SaveToFile();
        }

        public override void SaveToFile()
        {
            try
            {
                File.AppendAllText(logFilePath, LogContent + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to write [INFO] log to file: {ex.Message}");
            }
        }
    }
}