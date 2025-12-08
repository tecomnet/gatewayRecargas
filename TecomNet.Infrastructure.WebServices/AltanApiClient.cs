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
            // NOTA: El endpoint de MSISDN siempre usa producción, no sandbox
            var configuredBaseUrl = _configuration["AltanApi:BaseUrl"] 
                ?? "https://apigee-prod.altanredes.com/cm";
            
            // Para MSISDN, siempre usar producción (remover cm-sandbox si existe)
            var baseUrl = configuredBaseUrl;
            if (baseUrl.Contains("cm-sandbox", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = baseUrl.Replace("/cm-sandbox", "/cm", StringComparison.OrdinalIgnoreCase);
                _logger.LogInformation("MSISDN endpoint: Cambiando de sandbox a producción");
            }
            
            // Asegurar que BaseUrl no termine con /
            baseUrl = baseUrl.TrimEnd('/');
            
            // Construir endpoint completo
            var endpoint = $"{baseUrl}/v1/subscribers/msisdnInformation?msisdn={Uri.EscapeDataString(msisdn)}";

            // MSISDN siempre usa producción
            var isSandbox = false;
            var environment = "PRODUCCIÓN";

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Access token no proporcionado para consulta de MSISDN");
                throw new InvalidOperationException("Access token es requerido para consultar información de MSISDN");
            }

            _logger.LogInformation("=== CONSULTA MSISDN ===");
            _logger.LogInformation("Ambiente: {Environment} (MSISDN siempre usa producción)", environment);
            _logger.LogInformation("MSISDN: {Msisdn}", msisdn);
            _logger.LogInformation("Endpoint completo: {Endpoint}", endpoint);

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation("Enviando petición GET a: {Endpoint}", endpoint);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            _logger.LogInformation("Respuesta recibida - StatusCode: {StatusCode}, Content Length: {ContentLength}", 
                response.StatusCode, content?.Length ?? 0);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error obteniendo información de MSISDN. StatusCode: {StatusCode}, Content: {Content}", response.StatusCode, content);
                
                // Manejo de errores
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogError("=== ERROR 404 - URL NO ENCONTRADA ===");
                    _logger.LogError("Endpoint que causó el error: {Endpoint}", endpoint);
                    _logger.LogError("BaseUrl usado: {BaseUrl}", baseUrl);
                    throw new HttpRequestException($"Error 404: Recurso no encontrado - {content}. Endpoint: {endpoint}");
                }
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

    public async Task<PurchaseProductResponse> PurchaseProductAsync(PurchaseProductRequest request, string accessToken, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validar MSISDN (10 digitos)
            if (string.IsNullOrWhiteSpace(request.Msisdn) || request.Msisdn.Length != 10 || !request.Msisdn.All(char.IsDigit))
            {
                _logger.LogError("MSISDN inválido. Debe tener exactamente 10 dígitos. MSISDN recibido: {Msisdn}", request.Msisdn);
                throw new ArgumentException("MSISDN debe tener exactamente 10 dígitos", nameof(request));
            }

            // Validar que tenga al menos una oferta
            if (request.Offerings == null || request.Offerings.Count == 0)
            {
                _logger.LogError("No se proporcionaron ofertas para la compra");
                throw new ArgumentException("Debe incluir al menos una oferta", nameof(request));
            }

            // Construir endpoint usando BaseUrl
            var baseUrl = _configuration["AltanApi:BaseUrl"] 
                ?? "https://apigee-prod.altanredes.com/cm";
            
            // Asegurar que BaseUrl no termine con /
            baseUrl = baseUrl.TrimEnd('/');
            
            // Verificar si se está usando sandbox
            var isSandbox = baseUrl.Contains("cm-sandbox", StringComparison.OrdinalIgnoreCase);
            var environment = isSandbox ? "SANDBOX" : "PRODUCCIÓN";
            
            // Construir endpoint completo según documentación:
            // Sandbox: https://apigee-prod.altanredes.com/cm-sandbox/v1/products/purchase
            // Producción: https://apigee-prod.altanredes.com/cm/v1/products/purchase
            string endpoint;
            if (isSandbox)
            {
                // URL explícita para sandbox
                endpoint = "https://apigee-prod.altanredes.com/cm-sandbox/v1/products/purchase";
            }
            else
            {
                // URL para producción
                endpoint = "https://apigee-prod.altanredes.com/cm/v1/products/purchase";
            }
            
            _logger.LogInformation("Endpoint configurado: {Endpoint}", endpoint);
            _logger.LogInformation("Ambiente: {Environment}", environment);

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Access token no proporcionado para compra de producto");
                throw new InvalidOperationException("Access token es requerido para comprar producto");
            }

            // Limpiar el token de espacios en blanco (por si viene con espacios)
            accessToken = accessToken.Trim();
            
            // Log del token para verificar validez
            _logger.LogError("=== VERIFICACIÓN DE TOKEN ===");
            _logger.LogError("Token recibido: {Token}", accessToken);
            _logger.LogError("Token length: {Length} caracteres", accessToken.Length);
            _logger.LogError("Token vacío o nulo: {IsEmpty}", string.IsNullOrWhiteSpace(accessToken));

            _logger.LogInformation("=== COMPRA DE PRODUCTO ===");
            _logger.LogInformation("Ambiente: {Environment}", environment);
            _logger.LogInformation("MSISDN: {Msisdn}", request.Msisdn);
            _logger.LogInformation("Ofertas: {Offerings}", string.Join(", ", request.Offerings));
            _logger.LogInformation("idPoS: {IdPoS} (Longitud: {Length})", request.IdPoS, request.IdPoS?.Length ?? 0);
            _logger.LogInformation("Channel: {Channel}, Pipe: {Pipe}", request.ChannelOfSale, request.PipeOfSale);
            _logger.LogInformation("Endpoint completo: {Endpoint}", endpoint);
            _logger.LogInformation("BaseUrl configurado: {BaseUrl}", baseUrl);

            // Serializar el request a JSON respetando los JsonPropertyName attributes
            // Solo incluir los campos requeridos: msisdn, offerings, idPoS, channelOfSale, pipeOfSale
            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
                PropertyNamingPolicy = null // Respetar JsonPropertyName explícitamente
            });

            // Log del JSON enviado - usar LogError para asegurar que se vea
            _logger.LogError("=== JSON ENVIADO A ALTAN API (para debug) ===");
            _logger.LogError("JSON Request Body: {JsonContent}", jsonContent);
            
            // Validar que el JSON tenga el formato correcto
            try
            {
                var testDeserialize = JsonSerializer.Deserialize<PurchaseProductRequest>(jsonContent);
                _logger.LogInformation("JSON válido - puede ser deserializado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR: El JSON generado no puede ser deserializado de vuelta");
            }

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint);
            
            // Configurar header Authorization con formato exacto: "Bearer {token}"
            // AuthenticationHeaderValue automáticamente agrega el espacio entre "Bearer" y el token
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            
            // Configurar Content-Type exactamente como Postman (sin charset=utf-8)
            var requestContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            // Remover el charset para que sea exactamente "application/json" como en Postman
            requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
            {
                CharSet = null
            };
            requestMessage.Content = requestContent;
            
            // VERIFICACIÓN CRÍTICA: Asegurar que el header Authorization esté presente
            if (requestMessage.Headers.Authorization == null)
            {
                _logger.LogError("ERROR CRÍTICO: El header Authorization NO se configuró correctamente");
                throw new InvalidOperationException("El header Authorization no se pudo configurar");
            }
            
            // Log detallado de headers para verificar formato exacto - ANTES de enviar
            _logger.LogError("=== HEADERS QUE SE ENVIARÁN ===");
            _logger.LogError("Endpoint: {Endpoint}", endpoint);
            _logger.LogError("Método: POST");
            _logger.LogError("Authorization Header Scheme: {Scheme}", requestMessage.Headers.Authorization.Scheme);
            _logger.LogError("Authorization Header Parameter: {Parameter}", requestMessage.Headers.Authorization.Parameter);
            _logger.LogError("Authorization Header (ToString): {AuthHeader}", requestMessage.Headers.Authorization.ToString());
            _logger.LogError("Authorization Header (formato manual): Bearer {Token}", accessToken);
            _logger.LogError("Content-Type: {ContentType}", requestMessage.Content.Headers.ContentType?.ToString());
            _logger.LogError("Token length: {TokenLength} caracteres", accessToken?.Length ?? 0);
            
            // Verificar todos los headers que se enviarán
            _logger.LogError("=== TODOS LOS HEADERS ===");
            foreach (var header in requestMessage.Headers)
            {
                _logger.LogError("Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
            }
            _logger.LogError("=== HEADERS DE CONTENT ===");
            foreach (var header in requestMessage.Content.Headers)
            {
                _logger.LogError("Content Header: {Key} = {Value}", header.Key, string.Join(", ", header.Value));
            }

            _logger.LogError("Enviando petición POST a: {Endpoint}", endpoint);
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error al comprar producto. StatusCode: {StatusCode}, Content: {Content}", response.StatusCode, content);
                
                // Log detallado del error
                _logger.LogError("=== ANÁLISIS DEL ERROR ===");
                _logger.LogError("StatusCode: {StatusCode}", response.StatusCode);
                _logger.LogError("Response Content: {Content}", content);
                _logger.LogError("Token usado: {Token}", accessToken);
                _logger.LogError("MSISDN: {Msisdn}", request.Msisdn);
                _logger.LogError("Ofertas: {Offerings}", string.Join(", ", request.Offerings));
                _logger.LogError("idPoS: {IdPoS}", request.IdPoS);
                
                // Manejo de errores específicos
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    _logger.LogError("=== ERROR 400 - DATOS ENVIADOS ===");
                    _logger.LogError("JSON que causó el error: {JsonContent}", jsonContent);
                    _logger.LogError("Headers enviados:");
                    _logger.LogError("  - Authorization: Bearer {Token}", accessToken);
                    _logger.LogError("  - Content-Type: {ContentType}", requestMessage.Content.Headers.ContentType?.ToString());
                    _logger.LogError("Endpoint: {Endpoint}", endpoint);
                    _logger.LogError("Método: POST");
                    throw new HttpRequestException($"Error 400: Datos inválidos - {content}. JSON enviado: {jsonContent}");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new HttpRequestException($"Error 401: Token inválido - {content}");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new HttpRequestException($"Error 404: Endpoint incorrecto - {content}");
                }
                if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                {
                    throw new HttpRequestException($"Error 500: Error interno del servidor - {content}");
                }
                
                throw new HttpRequestException($"Error al comprar producto: {response.StatusCode} - {content}");
            }

            _logger.LogInformation("Compra de producto exitosa ({Environment}). StatusCode: {StatusCode}", environment, response.StatusCode);

            var purchaseResponse = JsonSerializer.Deserialize<PurchaseProductResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (purchaseResponse == null)
            {
                _logger.LogError("No se pudo deserializar la respuesta de compra. Content: {Content}", content);
                throw new InvalidOperationException("No se pudo deserializar la respuesta de compra");
            }

            _logger.LogInformation("Compra procesada correctamente ({Environment}). Order ID: {OrderId}, EffectiveDate: {EffectiveDate}", 
                environment, 
                purchaseResponse.Order?.Id ?? "N/A",
                purchaseResponse.EffectiveDate ?? "N/A");

            return purchaseResponse;
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
            _logger.LogError(ex, "Error inesperado al comprar producto");
            throw;
        }
    }
}

