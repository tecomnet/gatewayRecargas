using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecomNet.Domain.Models;

namespace TecomNet.Infrastructure.WebServices;

public class AltanApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AltanApiClient> _logger;

    public AltanApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<AltanApiClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TokenResponse> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = _configuration["AltanApi:TokenEndpoint"]
                ?? "https://apigee-prod.altanredes.com/v1/oauth/accesstoken?grant-type=client_credentials";

            var consumerKey = _configuration["AltanApi:ConsumerKey"] ?? string.Empty;
            var consumerSecret = _configuration["AltanApi:ConsumerSecret"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(consumerKey) || string.IsNullOrWhiteSpace(consumerSecret))
            {
                _logger.LogError("Las credenciales de AltanApi no están configuradas. Verifique ConsumerKey y ConsumerSecret en appsettings.json");
                throw new InvalidOperationException("Las credenciales de AltanApi no están configuradas. Verifique ConsumerKey y ConsumerSecret en appsettings.json");
            }

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumerKey}:{consumerSecret}"));

            _logger.LogInformation("Solicitando token de acceso a Altan API. Endpoint: {Endpoint}", endpoint);
            _logger.LogDebug("Authorization Basic generado: {Credentials}", credentials);

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error obteniendo token de Altan API. StatusCode: {StatusCode}, Content: {Content}", response.StatusCode, content);
                throw new HttpRequestException($"Error al obtener token de Altan API: {response.StatusCode} - {content}");
            }

            _logger.LogInformation("Token de acceso obtenido exitosamente. StatusCode: {StatusCode}", response.StatusCode);

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (tokenResponse == null)
            {
                _logger.LogError("No se pudo deserializar la respuesta del token. Content: {Content}", content);
                throw new InvalidOperationException("No se pudo deserializar la respuesta del token");
            }

            if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                _logger.LogError("El token de acceso está vacío en la respuesta");
                throw new InvalidOperationException("El token de acceso está vacío en la respuesta");
            }

            _logger.LogInformation("Token de acceso procesado correctamente. ClientId: {ClientId}, TokenType: {TokenType}, Status: {Status}", 
                tokenResponse.ClientId, tokenResponse.TokenType, tokenResponse.Status);

            return tokenResponse;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al obtener access token de Altan API");
            throw;
        }
    }
}

