using System.Text.Json.Serialization;

namespace backend___calculating.Models
{
    public class BruteForceRequest
    {
        [JsonPropertyName("userLogin")]
        public string UserLogin { get; set; } = string.Empty;

        [JsonPropertyName("passwordLength")]
        public int PasswordLength { get; set; }

        [JsonPropertyName("chars")]
        public string Chars { get; set; } = string.Empty;
    }
}