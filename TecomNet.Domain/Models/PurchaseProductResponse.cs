using System.Text.Json.Serialization;

namespace TecomNet.Domain.Models;

public class PurchaseProductResponse
{
    [JsonPropertyName("msisdn")]
    public string? Msisdn { get; set; }

    [JsonPropertyName("effectiveDate")]
    public string? EffectiveDate { get; set; }

    [JsonPropertyName("offerings")]
    public List<string>? Offerings { get; set; }

    [JsonPropertyName("order")]
    public OrderInfo? Order { get; set; }
}

public class OrderInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}




