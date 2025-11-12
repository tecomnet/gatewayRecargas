using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecomNet.Domain.Models;

namespace TecomNet.Infrastructure.WebApi.Clients;

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

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumerKey}:{consumerSecret}"));

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error obteniendo token: {StatusCode} - {Content}", response.StatusCode, content);
                throw new HttpRequestException($"Error al obtener token: {response.StatusCode} - {content}");
            }

            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return tokenResponse ?? throw new InvalidOperationException("No se pudo deserializar la respuesta del token");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener access token");
            throw;
        }
    }
}

