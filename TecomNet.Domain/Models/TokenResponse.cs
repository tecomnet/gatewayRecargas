using System.Text.Json.Serialization;

namespace TecomNet.Domain.Models
{
    public class TokenResponse
    {
        [JsonPropertyName("accesToken")]
        public string? AccesToken { get; set; }
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; }
        [JsonPropertyName("tokenType")]
        public string? TokenType { get; set; }
        [JsonPropertyName("issuedAt")]
        public string? IssuedAt { get; set; }
        [JsonPropertyName("expiresIn")]
        public string? ExpiresIn { get; set; }
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        [JsonPropertyName("scopes")]
        public string? Scopes { get; set; }
        public string? AccessToken { get; set; }

        public int GetExpiresInSeconds()
        {
            if (int.TryParse(ExpiresIn, out var seconds))
            {
                return seconds;
            }
            return 0;
        }
    }
}
