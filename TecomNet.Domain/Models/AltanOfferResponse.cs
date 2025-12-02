namespace TecomNet.Domain.Models;

public class AltanOfferResponse
{
    public string CommercialName { get; set; } = null!;
    public string IDOffer { get; set; } = null!;
    public decimal Price { get; set; }
    public Guid? MvnoId { get; set; }
}


