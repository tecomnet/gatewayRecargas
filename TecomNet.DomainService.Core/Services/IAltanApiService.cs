using System.Text.Json.Serialization;
using TecomNet.Domain.Models;

namespace TecomNet.DomainService.Core.Services
{
    public interface IAltanApiService
    {
        Task<TokenResponse> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    }
}
