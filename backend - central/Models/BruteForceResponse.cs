using System.Text.Json.Serialization;

namespace backend___central.Models
{
    public class BruteForceResponse
    {
        [JsonPropertyName("Message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("Password")]
        public string? Password { get; set; }

        [JsonPropertyName("Time")]
        public int Time { get; set; }
    }
}