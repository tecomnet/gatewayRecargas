using Microsoft.Extensions.Logging;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.WebServices;

namespace TecomNet.Domain.Service.Services
{
    public class AltanApiService : IAltanApiService
    {
        private readonly AltanApiClient _altanApiClient;
        private readonly ILogger<AltanApiService> _logger;
        private TokenResponse? _cachedTokenResponse;
        private DateTime _tokenExpiresAt;

        public AltanApiService(AltanApiClient altanApiClient, ILogger<AltanApiService> logger)
        {
            _altanApiClient = altanApiClient;
            _logger = logger;
            _tokenExpiresAt = DateTime.MinValue;
        }

        public async Task<TokenResponse> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_cachedTokenResponse != null && 
                    !string.IsNullOrEmpty(_cachedTokenResponse.AccessToken) && 
                    DateTime.UtcNow < _tokenExpiresAt)
                {
                    _logger.LogInformation("Usando token en caché");
                    var remainingSeconds = (int)(_tokenExpiresAt - DateTime.UtcNow).TotalSeconds;
                    _cachedTokenResponse.ExpiresIn = remainingSeconds.ToString();
                    return _cachedTokenResponse;
                }

                _logger.LogInformation("Obteniendo nuevo token de acceso");
                var token = await _altanApiClient.GetAccessTokenAsync(cancellationToken);
                _cachedTokenResponse = token;
                var expiresInSeconds = token.GetExpiresInSeconds();
                _tokenExpiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, expiresInSeconds - 60));

                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener access token");
                throw;
            }
        }

        public async Task<MsisdnInformationResponse> GetMsisdnInformationAsync(string msisdn, CancellationToken cancellationToken = default)
        {
            try
            {
                // Obtener token de acceso (usará caché si está disponible)
                var token = await GetAccessTokenAsync(cancellationToken);
                
                if (string.IsNullOrWhiteSpace(token.AccessToken))
                {
                    _logger.LogError("No se pudo obtener un token de acceso válido para consultar MSISDN");
                    throw new InvalidOperationException("No se pudo obtener un token de acceso válido");
                }

                _logger.LogInformation("Consultando información de MSISDN: {Msisdn}", msisdn);
                var msisdnInfo = await _altanApiClient.GetMsisdnInformationAsync(msisdn, token.AccessToken, cancellationToken);
                
                return msisdnInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información de MSISDN");
                throw;
            }
        }

        public async Task<MsisdnInformationResponse> GetMsisdnInformationAsync(string msisdn, string accessToken, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogError("Token de acceso no proporcionado para consultar MSISDN");
                    throw new ArgumentException("Token de acceso es requerido", nameof(accessToken));
                }

                _logger.LogInformation("Consultando información de MSISDN con token proporcionado: {Msisdn}", msisdn);
                var msisdnInfo = await _altanApiClient.GetMsisdnInformationAsync(msisdn, accessToken, cancellationToken);
                
                return msisdnInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información de MSISDN con token proporcionado");
                throw;
            }
        }
    }
}

