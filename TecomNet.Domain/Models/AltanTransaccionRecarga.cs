namespace TecomNet.Domain.Models;

public class AltanTransaccionRecarga
{
    public long IdTransaccion { get; set; }
    
    public DateTime? InicioTransaccionCanalDeVenta { get; set; }
    public DateTime? InicioTransaccionAltan { get; set; }
    public DateTime? FinTransaccionAltan { get; set; }
    public DateTime? FinTransaccionCanalDeVenta { get; set; }
    
    public string BE { get; set; } = null!;
    public string MSISDN { get; set; } = null!;
    public decimal MontoRecarga { get; set; }
    public string OfferId { get; set; } = null!;
    public string CanalDeVenta { get; set; } = null!;
    public string Medio { get; set; } = null!;
    public string? IdPOS { get; set; }
    public string? OrderId { get; set; }
    public string? ResultadoTransaccion { get; set; }
    
    // Campos de reversa
    public DateTime? InicioTransaccionAltanReversa { get; set; }
    public DateTime? FinTransaccionAltanReversa { get; set; }
    public DateTime? FinTransaccionCanalDeVentaReversa { get; set; }
    public decimal? MontoRecargaReversa { get; set; }
    public string? OrderIdReversa { get; set; }
    public string? ResultadoTransaccionReversa { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

