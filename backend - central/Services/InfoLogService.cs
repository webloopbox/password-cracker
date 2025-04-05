using System;
using System.IO;

namespace backend___central.Services
{
    public class InfoLogService : ILogService
    {
        private string LogContent { get; set; } = "";
        private readonly string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs-backend-central.txt");

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
                using FileStream fileStream = new(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using StreamWriter writer = new(fileStream);
                writer.WriteLine(LogContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to write [INFO] log to file: {ex.Message}");
            }
        }
    }
}