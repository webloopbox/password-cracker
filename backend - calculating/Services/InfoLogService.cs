using System;
using System.IO;
using backend___calculating.Interfaces;

namespace backend___calculating.Services
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
                File.AppendAllText(logFilePath, LogContent + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to write [INFO] log to file: {ex.Message}");
            }
        }
    }
}