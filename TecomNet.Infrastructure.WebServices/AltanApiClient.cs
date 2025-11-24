using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
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
            var tokenEndpoint = _configuration["AltanApi:TokenEndpoint"];
            string endpoint;
            string baseUrl;

            if (!string.IsNullOrWhiteSpace(tokenEndpoint))
            {
                endpoint = tokenEndpoint;
                var uri = new Uri(endpoint);
                baseUrl = $"{uri.Scheme}://{uri.Host}";
            }
            else
            {
                baseUrl = _configuration["AltanApi:BaseUrl"] 
                    ?? "https://apigee-prod.altanredes.com/cm";
     
                baseUrl = baseUrl.TrimEnd('/');
                
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

    public async Task<MsisdnInformationResponse> GetMsisdnInformationAsync(string msisdn, string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validar MSISDN (10 digitos)
            if (string.IsNullOrWhiteSpace(msisdn) || msisdn.Length != 10 || !msisdn.All(char.IsDigit))
            {
                _logger.LogError("MSISDN inválido. Debe tener exactamente 10 dígitos. MSISDN recibido: {Msisdn}", msisdn);
                throw new ArgumentException("MSISDN debe tener exactamente 10 dígitos", nameof(msisdn));
            }

            // Construir endpoint usando BaseUrl
            var baseUrl = _configuration["AltanApi:BaseUrl"] 
                ?? "https://apigee-prod.altanredes.com/cm";
            
            // Asegurar que BaseUrl no termine con /
            baseUrl = baseUrl.TrimEnd('/');
            
            // Construir endpoint completo
            var endpoint = $"{baseUrl}/v1/subscribers/msisdnInformation?msisdn={msisdn}";

            // Verificar si se esta usando sandbox
            var isSandbox = baseUrl.Contains("cm-sandbox", StringComparison.OrdinalIgnoreCase);
            var environment = isSandbox ? "SANDBOX" : "PRODUCCIÓN";

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Access token no proporcionado para consulta de MSISDN");
                throw new InvalidOperationException("Access token es requerido para consultar información de MSISDN");
            }

            _logger.LogInformation("=== CONSULTA MSISDN ===");
            _logger.LogInformation("Ambiente: {Environment}", environment);
            _logger.LogInformation("MSISDN: {Msisdn}", msisdn);
            _logger.LogInformation("Endpoint: {Endpoint}", endpoint);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error obteniendo información de MSISDN. StatusCode: {StatusCode}, Content: {Content}", response.StatusCode, content);
                
                // Manejo de errores
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    throw new HttpRequestException($"Error 400: Solicitud inválida - {content}");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new HttpRequestException($"Error 401: Token inválido - {content}");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    throw new HttpRequestException($"Error 500: Error interno del servidor - {content}");
                }
                
                throw new HttpRequestException($"Error al obtener información de MSISDN: {response.StatusCode} - {content}");
            }

            _logger.LogInformation("Información de MSISDN obtenida exitosamente desde {Environment}. StatusCode: {StatusCode}", environment, response.StatusCode);

            var msisdnInfo = JsonSerializer.Deserialize<MsisdnInformationResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (msisdnInfo == null)
            {
                _logger.LogError("No se pudo deserializar la respuesta de MSISDN. Content: {Content}", content);
                throw new InvalidOperationException("No se pudo deserializar la respuesta de MSISDN");
            }

            _logger.LogInformation("Información de MSISDN procesada correctamente ({Environment}). BE: {BeId}, IDA: {Ida}, Product: {Product}", 
                environment, 
                msisdnInfo.ResponseMsisdn?.Information?.BeId ?? "N/A", 
                msisdnInfo.ResponseMsisdn?.Information?.Ida ?? "N/A", 
                msisdnInfo.ResponseMsisdn?.Information?.Product ?? "N/A");

            return msisdnInfo;
        }
        catch (ArgumentException)
        {
            throw;
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
            _logger.LogError(ex, "Error inesperado al obtener información de MSISDN");
            throw;
        }
    }
}

