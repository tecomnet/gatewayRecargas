using System.Text.Json.Serialization;

namespace TecomNet.Domain.Models
{
    public class MsisdnInformationResponse
    {
        [JsonPropertyName("responseMsisdn")]
        public ResponseMsisdn? ResponseMsisdn { get; set; }
    }

    public class ResponseMsisdn
    {
        [JsonPropertyName("information")]
        public MsisdnInformation? Information { get; set; }
    }

    public class MsisdnInformation
    {
        [JsonPropertyName("beId")]
        public string? BeId { get; set; }

        [JsonPropertyName("product")]
        public string? Product { get; set; }

        [JsonPropertyName("ida")]
        public string? Ida { get; set; }
    }
}


