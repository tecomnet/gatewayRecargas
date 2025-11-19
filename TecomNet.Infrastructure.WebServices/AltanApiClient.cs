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
            // Construir endpoint dinámicamente usando BaseUrl o usar TokenEndpoint completo si está configurado
            var tokenEndpoint = _configuration["AltanApi:TokenEndpoint"];
            string endpoint;
            string baseUrl;

            if (!string.IsNullOrWhiteSpace(tokenEndpoint))
            {
                // Si TokenEndpoint está configurado completamente, usarlo (compatibilidad hacia atrás)
                endpoint = tokenEndpoint;
                // Extraer BaseUrl del endpoint para verificación
                var uri = new Uri(endpoint);
                baseUrl = $"{uri.Scheme}://{uri.Host}";
            }
            else
            {
                // Construir endpoint usando BaseUrl
                baseUrl = _configuration["AltanApi:BaseUrl"] 
                    ?? "https://apigee-prod.altanredes.com/cm";
                
                // Asegurar que BaseUrl no termine con /
                baseUrl = baseUrl.TrimEnd('/');
                
                // El endpoint de token OAuth siempre está en la raíz de la API
                // Removemos /cm-sandbox o /cm del BaseUrl para construir el endpoint de token
                var tokenBaseUrl = baseUrl;
                if (tokenBaseUrl.Contains("/cm-sandbox", StringComparison.OrdinalIgnoreCase))
                {
                    tokenBaseUrl = tokenBaseUrl.Replace("/cm-sandbox", "", StringComparison.OrdinalIgnoreCase);
                }
                else if (tokenBaseUrl.Contains("/cm", StringComparison.OrdinalIgnoreCase) && !tokenBaseUrl.Contains("/cm-sandbox", StringComparison.OrdinalIgnoreCase))
                {
                    tokenBaseUrl = tokenBaseUrl.Replace("/cm", "", StringComparison.OrdinalIgnoreCase);
                }
                
                endpoint = $"{tokenBaseUrl}/v1/oauth/accesstoken?grant-type=client_credentials";
            }

            // Verificar si se está usando sandbox
            // Sandbox tiene /cm-sandbox, Producción tiene /cm
            var isSandbox = baseUrl.Contains("cm-sandbox", StringComparison.OrdinalIgnoreCase);
            var environment = isSandbox ? "SANDBOX" : "PRODUCCIÓN";

            var consumerKey = _configuration["AltanApi:ConsumerKey"] ?? string.Empty;
            var consumerSecret = _configuration["AltanApi:ConsumerSecret"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(consumerKey) || string.IsNullOrWhiteSpace(consumerSecret))
            {
                _logger.LogError("Las credenciales de AltanApi no están configuradas. Verifique ConsumerKey y ConsumerSecret en appsettings.json");
                throw new InvalidOperationException("Las credenciales de AltanApi no están configuradas. Verifique ConsumerKey y ConsumerSecret en appsettings.json");
            }

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumerKey}:{consumerSecret}"));

            _logger.LogInformation("=== CONFIGURACIÓN ALTAN API ===");
            _logger.LogInformation("Ambiente: {Environment}", environment);
            _logger.LogInformation("BaseUrl: {BaseUrl}", baseUrl);
            _logger.LogInformation("Endpoint completo: {Endpoint}", endpoint);
            _logger.LogInformation("Solicitando token de acceso a Altan API ({Environment})", environment);
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

            _logger.LogInformation("Token de acceso obtenido exitosamente desde {Environment}. StatusCode: {StatusCode}", environment, response.StatusCode);

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

            _logger.LogInformation("Token de acceso procesado correctamente ({Environment}). ClientId: {ClientId}, TokenType: {TokenType}, Status: {Status}", 
                environment, tokenResponse.ClientId, tokenResponse.TokenType, tokenResponse.Status);

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

