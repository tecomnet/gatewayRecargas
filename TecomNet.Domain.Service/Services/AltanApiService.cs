using Microsoft.Extensions.Logging;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core;
using TecomNet.Infrastructure.WebApi.Clients;

namespace TecomNet.Domain.Service.Services
{
    public class AltanApiService : IAltanApiService
    {
        private readonly AltanApiClient _altanApiClient;
        private readonly ILogger<AltanApiService> _logger;
        private string? _cachedToken;
        private DateTime _tokenExpiresAt;

        public AltanApiService(AltanApiClient altanApiClient, ILogger<AltanApiService> logger)
        {
            _altanApiClient = altanApiClient;
            _logger = logger;
        }
        public async Task<TokenResponse> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await _altanApiClient.GetAccessTokenAsync(cancellationToken);
                _cachedToken = token.AccessToken;
                var expiresInSeconds = token.GetExpiresInSeconds();
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds - 60);

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Al Obtener Acces Token");
                throw;
            }
        }
    }
}

