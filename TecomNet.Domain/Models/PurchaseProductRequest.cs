using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TecomNet.Domain.Models;

public class PurchaseProductRequest
{
    [Required(ErrorMessage = "MSISDN es requerido")]
    [StringLength(10, MinimumLength = 10, ErrorMessage = "MSISDN debe tener exactamente 10 dígitos")]
    [JsonPropertyName("msisdn")]
    public string Msisdn { get; set; } = null!;

    [Required(ErrorMessage = "Offerings es requerido")]
    [MinLength(1, ErrorMessage = "Debe incluir al menos una oferta")]
    [JsonPropertyName("offerings")]
    public List<string> Offerings { get; set; } = new();

    [Required(ErrorMessage = "idPoS es requerido")]
    [StringLength(15, ErrorMessage = "idPoS debe tener máximo 15 caracteres")]
    [JsonPropertyName("idPoS")]
    public string IdPoS { get; set; } = null!;

    [Required(ErrorMessage = "channelOfSale es requerido")]
    [JsonPropertyName("channelOfSale")]
    public string ChannelOfSale { get; set; } = null!;

    [Required(ErrorMessage = "pipeOfSale es requerido")]
    [JsonPropertyName("pipeOfSale")]
    public string PipeOfSale { get; set; } = null!;
}

