namespace backend___central.Services
{
    public interface ILogService
    {
        void SaveToFile();
        void LogMessage(string message);
        static string GetCurrentDate()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
        static void LogInfo(IEnumerable<ILogService> logServices, string message)
        {
            InfoLogService? infoLogService = logServices?.OfType<InfoLogService>().FirstOrDefault();
            infoLogService?.LogMessage($"{message}");
        }

        static void LogError(IEnumerable<ILogService> logServices, string message)
        {
            ErrorLogService? errorLogService = logServices?.OfType<ErrorLogService>().FirstOrDefault();
            errorLogService?.LogMessage($"{message}");
        }
    }
}