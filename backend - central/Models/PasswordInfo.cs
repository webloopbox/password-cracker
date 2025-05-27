namespace backend___central.Models
{
    public class PasswordInfo
    {
        public string Value { get; set; } = "";
        public string ServerIp { get; set; } = "";
        public int ServerTime { get; set; }
        public int ProcessingTime { get; set; }
        public int TotalTime { get; set; }
    }
}