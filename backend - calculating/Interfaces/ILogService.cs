using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using backend___calculating.Services;

namespace backend___calculating.Interfaces
{
    public abstract class ILogService
    {
        public static readonly string logFilePath = Path.Combine(Directory.GetCurrentDirectory(), "logs-backend-calculating.txt");
        public abstract void SaveToFile();
        public abstract void LogMessage(string message);
        public static string GetCurrentDate()
        {
            try 
            {
                string tzId = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                    ? "Central European Standard Time"
                    : "Europe/Warsaw";
                TimeZoneInfo warsawZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                DateTime warsawTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, warsawZone);
                return warsawTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
            catch (TimeZoneNotFoundException)
            {
                return DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
        }
        public static void LogInfo(IEnumerable<ILogService> logServices, string message)
        {
            ILogService? infoLogService = logServices?.FirstOrDefault(logService => logService is InfoLogService);
            infoLogService?.LogMessage($"{message}");
        }
        public static void LogError(IEnumerable<ILogService> logServices, string message)
        {
            ILogService? errorLogService = logServices?.FirstOrDefault(logService => logService is ErrorLogService);
            errorLogService?.LogMessage($"{message}");
        }
    }
}