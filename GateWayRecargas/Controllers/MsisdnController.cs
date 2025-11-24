using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;

namespace GateWayRecargas.Controllers
{
    [ApiController]
    [Route("/api/v1.0/[controller]")]
    public class MsisdnController : ControllerBase
    {
        private readonly IAltanApiService _altanApiService;
        private readonly ILogger<MsisdnController> _logger;

        public MsisdnController(IAltanApiService altanApiService, ILogger<MsisdnController> logger)
        {
            _altanApiService = altanApiService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la información asociada a un número MSISDN
        /// </summary>
        /// <param name="msisdn">Número MSISDN de 10 digitos</param>
        /// <param name="token">Token de acceso Bearer (opcional). Si no se proporciona, se obtendrá automáticamente.</param>
        /// <param name="cancellationToken">Token de cancelación</param>
        /// <returns>Información del MSISDN (BE, IDA, ProductType)</returns>
        [HttpGet("msisdnInformation")]
        [ProducesResponseType(typeof(MsisdnInformationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<MsisdnInformationResponse>> GetMsisdnInformation(
            [FromQuery, Required, StringLength(10, MinimumLength = 10)] string msisdn,
            [FromQuery] string? token = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Intentar obtener el token del header Authorization si no viene en query
                string? accessToken = token;
                
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    // Intentar obtener del header Authorization
                    var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        accessToken = authHeader.Substring("Bearer ".Length).Trim();
                        _logger.LogInformation("Token obtenido del header Authorization");
                    }
                }

                MsisdnInformationResponse msisdnInfo;

                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogInformation("Solicitando información de MSISDN con token proporcionado: {Msisdn}", msisdn);
                    msisdnInfo = await _altanApiService.GetMsisdnInformationAsync(msisdn, accessToken, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("Solicitando información de MSISDN (obteniendo token automáticamente): {Msisdn}", msisdn);
                    msisdnInfo = await _altanApiService.GetMsisdnInformationAsync(msisdn, cancellationToken);
                }
                
                return Ok(msisdnInfo);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "MSISDN inválido: {Msisdn}", msisdn);
                return BadRequest(new
                {
                    error = "MSISDN inválido",
                    message = ex.Message
                });
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401"))
            {
                _logger.LogError(ex, "Token inválido al consultar MSISDN");
                return StatusCode(StatusCodes.Status401Unauthorized, new
                {
                    error = "Token inválido",
                    message = ex.Message
                });
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("400"))
            {
                _logger.LogWarning(ex, "Solicitud inválida para MSISDN: {Msisdn}", msisdn);
                return BadRequest(new
                {
                    error = "Solicitud inválida",
                    message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información de MSISDN: {Msisdn}", msisdn);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error al obtener información de MSISDN",
                    message = ex.Message
                });
            }
        }
    }
}

