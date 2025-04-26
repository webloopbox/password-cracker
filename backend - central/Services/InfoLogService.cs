using System;
using System.IO;
using backend___central.Interfaces;

namespace backend___central.Services
{
    public class InfoLogService : ILogService
    {
        private string LogContent { get; set; } = "";

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