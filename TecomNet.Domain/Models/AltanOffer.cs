namespace TecomNet.Domain.Models;

public class AltanOffer
{
    public Guid Id { get; set; }                     // PK
    public string CommercialName { get; set; } = null!;
    public string IDOffer { get; set; } = null!;
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Guid? MvnoId { get; set; }
    
    // Navigation property
    public Mvno? Mvno { get; set; }
}