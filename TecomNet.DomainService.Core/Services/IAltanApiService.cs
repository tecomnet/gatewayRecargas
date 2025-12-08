using System.Text.Json.Serialization;
using TecomNet.Domain.Models;

namespace TecomNet.DomainService.Core.Services
{
    public interface IAltanApiService
    {
        Task<TokenResponse> GetAccessTokenAsync(CancellationToken cancellationToken = default);
        Task<MsisdnInformationResponse> GetMsisdnInformationAsync(string msisdn, CancellationToken cancellationToken = default);
        Task<MsisdnInformationResponse> GetMsisdnInformationAsync(string msisdn, string accessToken, CancellationToken cancellationToken = default);
        Task<PurchaseProductResponse> PurchaseProductAsync(PurchaseProductRequest request, CancellationToken cancellationToken = default);
        Task<PurchaseProductResponse> PurchaseProductAsync(PurchaseProductRequest request, string accessToken, CancellationToken cancellationToken = default);
    }
}
