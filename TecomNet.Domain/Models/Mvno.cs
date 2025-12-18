namespace TecomNet.Domain.Models;

public class Mvno
{
    public Guid Id { get; set; }
    public string BeId { get; set; } = null!;
    
    // Navigation property
    public ICollection<AltanOffer> AltanOffers { get; set; } = new List<AltanOffer>();
}















