using System.Text.Json.Serialization;

namespace backend___calculating.Models
{
    public class BruteForceResponse
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string? Password { get; set; } = null;

        [JsonPropertyName("time")]
        public int Time { get; set; }

        [JsonPropertyName("calculationTime")]
        public int CalculationTime { get; set; }
    }
}