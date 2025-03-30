namespace backend___central.Services
{
    public interface ILogService
    {
        void LogMessage(string message);
        static string GetCurrentDate()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }
    }
}