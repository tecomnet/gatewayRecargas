namespace TecomNet.Domain.Models;

public class AltanTransaccionRecarga
{
    public long IdTransaccion { get; set; }
    
    public DateTime InicioTransaccionCanalDeVenta { get; set; }
    public DateTime InicioTransaccionAltan { get; set; }
    public DateTime? FinTransaccionAltan { get; set; }
    public DateTime? FinTransaccionCanalDeVenta { get; set; }
    
    public string BE { get; set; } = null!;
    public string MSISDN { get; set; } = null!;
    public decimal MontoRecarga { get; set; }
    public string OfferId { get; set; } = null!;
    public string CanalDeVenta { get; set; } = null!;
    public string Medio { get; set; } = null!;
    public string IdPOS { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public string ResultadoTransaccion { get; set; } = null!;
    
    // Campos de reversa
    public bool? AplicaReverso { get; set; }
    public DateTime? InicioTransaccionReversa { get; set; }
    public DateTime? InicioTransaccionReversaAltan { get; set; }
    public DateTime? FinTransaccionReversaAltan { get; set; }
    public DateTime? FinTransaccionReversa { get; set; }
    public string? OrderIdReversa { get; set; }
    public string? ResultadoTransaccionReversa { get; set; }
    
    public DateTime CreatedAt { get; set; }
}

