using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.Sql;

namespace GateWayRecargas.Controllers;

[ApiController]
[Route("/api/v1.0/[controller]")]
[Tags("2. Altan")]
[Authorize] // Requiere autenticación JWT
public class AltanController : ControllerBase
{
    private readonly IAltanApiService _altanApiService;
    private readonly TecomNetDbContext _context;
    private readonly ILogger<AltanController> _logger;

    public AltanController(
        IAltanApiService altanApiService,
        TecomNetDbContext context,
        ILogger<AltanController> logger)
    {
        _altanApiService = altanApiService;
        _context = context;
        _logger = logger;
    }

    #region Token

    /// <summary>
    /// Obtiene el token de acceso de Altan
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Token de acceso de Altan</returns>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TokenResponse>> GetToken(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Solicitando token de acceso");
            var tokenResponse = await _altanApiService.GetAccessTokenAsync(cancellationToken);
            return Ok(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener token de acceso");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al obtener token de acceso",
                message = ex.Message
            });
        }
    }

    #endregion

    #region Ofertas

    /// <summary>
    /// Obtiene las ofertas de Altan filtradas por BeId
    /// </summary>
    /// <param name="beId">Identificador BE (Business Entity ID)</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Lista de ofertas (CommercialName, IDOffer, Price, MvnoId)</returns>
    [HttpGet("offers/byBeId")]
    [ProducesResponseType(typeof(List<AltanOfferResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<AltanOfferResponse>>> GetOffersByBeId(
        [FromQuery, Required] string beId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(beId))
            {
                _logger.LogWarning("BeId no proporcionado o vacío");
                return BadRequest(new
                {
                    error = "BeId requerido",
                    message = "El parámetro beId es obligatorio"
                });
            }

            _logger.LogInformation("Buscando ofertas por BeId: {BeId}", beId);

            var startTime = DateTime.UtcNow;
            
            FormattableString sqlQuery = $@"
                SELECT ao.Id, ao.CommercialName, ao.IDOffer, ao.Price, ao.IsActive, 
                       ao.CreatedAt, ao.UpdatedAt, ao.MvnoId
                FROM dbo.AltanOffers ao
                INNER JOIN dbo.Mvnos m ON ao.MvnoId = m.Id
                WHERE m.BeId = {beId}
                AND ao.IsActive = 1
            ";
            
            var offers = await _context.AltanOffers
                .FromSql(sqlQuery)
                .Select(ao => new AltanOfferResponse
                {
                    CommercialName = ao.CommercialName,
                    IDOffer = ao.IDOffer,
                    Price = ao.Price,
                    MvnoId = ao.MvnoId
                })
                .ToListAsync(cancellationToken);

            var elapsedTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Consulta completada en {ElapsedMs}ms para BeId: {BeId}", elapsedTime, beId);

            if (offers == null || offers.Count == 0)
            {
                _logger.LogWarning("No se encontraron ofertas para BeId: {BeId}", beId);
                return NotFound(new
                {
                    error = "No se encontraron ofertas",
                    message = $"No se encontraron ofertas activas para el BeId: {beId}"
                });
            }

            _logger.LogInformation("Se encontraron {Count} ofertas para BeId: {BeId}", offers.Count, beId);
            return Ok(offers);
        }
        catch (Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            _logger.LogError(sqlEx, "Error SQL al buscar ofertas por BeId: {BeId}. Error Number: {ErrorNumber}, State: {State}", 
                beId, sqlEx.Number, sqlEx.State);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error de base de datos",
                message = sqlEx.Message,
                errorNumber = sqlEx.Number,
                state = sqlEx.State
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar ofertas por BeId: {BeId}. StackTrace: {StackTrace}", 
                beId, ex.StackTrace);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al obtener ofertas",
                message = ex.Message,
                innerException = ex.InnerException?.Message
            });
        }
    }

    #endregion

    #region MSISDN

    /// <summary>
    /// Obtiene la información asociada a un número MSISDN
    /// </summary>
    /// <param name="msisdn">Número MSISDN de 10 digitos</param>
    /// <param name="token">Token de acceso Bearer (opcional). Si no se proporciona, se obtendrá automáticamente.</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Información del MSISDN (BE, IDA, ProductType)</returns>
    [HttpGet("msisdn/information")]
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
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _logger.LogError(ex, "Endpoint no encontrado para MSISDN: {Msisdn}", msisdn);
            return StatusCode(StatusCodes.Status404NotFound, new
            {
                error = "Endpoint no encontrado",
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

    #endregion

    #region Recargas

    /// <summary>
    /// Compra/Activa un producto (oferta) sobre un MSISDN
    /// </summary>
    /// <param name="request">Datos de la compra:
    /// - msisdn: Número MSISDN de 10 dígitos
    /// - offerings: Array de IDs de ofertas (ej: ["18799011114"])
    /// - idPoS: Identificador del punto de venta (ej: "TecomNet")
    /// - channelOfSale: Canal de venta (se normaliza a "RETAILER" si se proporciona otro valor)
    /// - pipeOfSale: Pipeline de venta (se normaliza a "GATEWAY_RECARGA" si se proporciona otro valor)
    /// </param>
    /// <param name="token">Token de acceso Bearer (opcional). Si no se proporciona, se obtendrá automáticamente.</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Respuesta de la compra con order ID y effectiveDate</returns>
    [HttpPost("purchase")]
    [ProducesResponseType(typeof(PurchaseProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PurchaseProductResponse>> PurchaseProduct(
        [FromBody] PurchaseProductRequest request,
        [FromQuery] string? token = null,
        CancellationToken cancellationToken = default)
    {
        var inicioTransaccionCanalDeVenta = DateTime.UtcNow;
        DateTime? inicioTransaccionAltan = null;
        DateTime? finTransaccionAltan = null;
        
        string? beId = null;
        decimal montoRecarga = 0;
        string offerId = string.Empty;
        
        try
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Datos de compra inválidos. Errores: {Errors}", 
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(new
                {
                    error = "Datos inválidos",
                    message = "La solicitud contiene datos inválidos",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            request.ChannelOfSale = request.ChannelOfSale?.ToUpperInvariant() ?? "RETAILER";
            request.PipeOfSale = request.PipeOfSale?.ToUpperInvariant() ?? "GATEWAY_RECARGA";
            
            var allowedChannels = new[] { "RETAILER" };
            var allowedPipes = new[] { "GATEWAY_RECARGA" };
            
            if (!allowedChannels.Contains(request.ChannelOfSale))
            {
                _logger.LogWarning("ChannelOfSale inválido: {Channel}. Usando RETAILER por defecto", request.ChannelOfSale);
                request.ChannelOfSale = "RETAILER";
            }
            
            if (!allowedPipes.Contains(request.PipeOfSale))
            {
                _logger.LogWarning("PipeOfSale inválido: {Pipe}. Usando GATEWAY_RECARGA por defecto", request.PipeOfSale);
                request.PipeOfSale = "GATEWAY_RECARGA";
            }

            offerId = request.Offerings?.FirstOrDefault() ?? string.Empty;
            
            if (string.IsNullOrEmpty(offerId))
            {
                _logger.LogWarning("No se proporcionó OfferId en la lista de ofertas. Offerings: {Offerings}",
                    string.Join(", ", request.Offerings ?? new List<string>()));
            }

            if (!string.IsNullOrEmpty(offerId))
            {
                var oferta = await _context.AltanOffers
                    .FirstOrDefaultAsync(o => o.IDOffer == offerId && o.IsActive, cancellationToken);
                
                if (oferta != null)
                {
                    montoRecarga = oferta.Price;
                    _logger.LogInformation("Monto de oferta obtenido: {Monto} para OfferId: {OfferId}", montoRecarga, offerId);
                }
                else
                {
                    _logger.LogWarning("No se encontró oferta activa con IDOffer: {OfferId}. Usando monto 0", offerId);
                }
            }
            else
            {
                _logger.LogError("OfferId está vacío. No se puede obtener el monto de la oferta.");
            }

            try
            {
                var msisdnInfo = await _altanApiService.GetMsisdnInformationAsync(request.Msisdn, cancellationToken);
                beId = msisdnInfo?.ResponseMsisdn?.Information?.BeId;
                _logger.LogInformation("BE obtenido del MSISDN: {BE}", beId ?? "N/A");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo obtener BE del MSISDN {Msisdn}. Continuando sin BE", request.Msisdn);
            }

            string? accessToken = token?.Trim();
            
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    accessToken = authHeader.Substring("Bearer ".Length).Trim();
                    _logger.LogInformation("Token obtenido del header Authorization");
                }
            }
            
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                accessToken = accessToken.Trim();
            }

            inicioTransaccionAltan = DateTime.UtcNow;

            PurchaseProductResponse purchaseResponse;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogInformation("Comprando producto con token proporcionado. MSISDN: {Msisdn}, Ofertas: {Offerings}", 
                    request.Msisdn, string.Join(", ", request.Offerings ?? new List<string>()));
                purchaseResponse = await _altanApiService.PurchaseProductAsync(request, accessToken, cancellationToken);
            }
            else
            {
                _logger.LogInformation("Comprando producto (obteniendo token automáticamente). MSISDN: {Msisdn}, Ofertas: {Offerings}", 
                    request.Msisdn, string.Join(", ", request.Offerings ?? new List<string>()));
                purchaseResponse = await _altanApiService.PurchaseProductAsync(request, cancellationToken);
            }
            
            finTransaccionAltan = DateTime.UtcNow;
            
            _logger.LogInformation("Compra exitosa. Order ID: {OrderId}, EffectiveDate: {EffectiveDate}", 
                purchaseResponse.Order?.Id, purchaseResponse.EffectiveDate);
            
            await GuardarTransaccionEnBD(
                inicioTransaccionCanalDeVenta,
                inicioTransaccionAltan ?? inicioTransaccionCanalDeVenta,
                finTransaccionAltan,
                beId ?? "N/A",
                request.Msisdn,
                montoRecarga,
                offerId,
                request.ChannelOfSale ?? "RETAILER",
                request.PipeOfSale ?? "GATEWAY_RECARGA",
                request.IdPoS ?? "N/A",
                purchaseResponse.Order?.Id ?? "N/A",
                "EXITOSO",
                cancellationToken);
            
            return Ok(purchaseResponse);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Datos inválidos para compra. MSISDN: {Msisdn}", request?.Msisdn ?? "N/A");
            
            await GuardarTransaccionEnBD(
                inicioTransaccionCanalDeVenta,
                inicioTransaccionAltan ?? inicioTransaccionCanalDeVenta,
                finTransaccionAltan ?? DateTime.UtcNow,
                beId ?? "N/A",
                request?.Msisdn ?? "N/A",
                montoRecarga,
                offerId,
                request?.ChannelOfSale ?? "N/A",
                request?.PipeOfSale ?? "N/A",
                request?.IdPoS ?? "N/A",
                "N/A",
                "ERROR_VALIDACION",
                cancellationToken);
            
            return BadRequest(new
            {
                error = "Datos inválidos",
                message = ex.Message
            });
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            _logger.LogError(ex, "Token inválido al comprar producto");
            
            await GuardarTransaccionEnBD(
                inicioTransaccionCanalDeVenta,
                inicioTransaccionAltan ?? inicioTransaccionCanalDeVenta,
                finTransaccionAltan ?? DateTime.UtcNow,
                beId ?? "N/A",
                request.Msisdn,
                montoRecarga,
                offerId,
                request.ChannelOfSale,
                request.PipeOfSale,
                request.IdPoS ?? "N/A",
                "N/A",
                "ERROR_401",
                cancellationToken);
            
            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                error = "Token inválido",
                message = ex.Message
            });
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("400"))
        {
            _logger.LogWarning(ex, "Solicitud inválida para compra. MSISDN: {Msisdn}", request.Msisdn);
            
            await GuardarTransaccionEnBD(
                inicioTransaccionCanalDeVenta,
                inicioTransaccionAltan ?? inicioTransaccionCanalDeVenta,
                finTransaccionAltan ?? DateTime.UtcNow,
                beId ?? "N/A",
                request.Msisdn,
                montoRecarga,
                offerId,
                request.ChannelOfSale,
                request.PipeOfSale,
                request.IdPoS ?? "N/A",
                "N/A",
                "ERROR_400",
                cancellationToken);
            
            return BadRequest(new
            {
                error = "Solicitud inválida",
                message = ex.Message
            });
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            _logger.LogError(ex, "Endpoint incorrecto al comprar producto");
            
            await GuardarTransaccionEnBD(
                inicioTransaccionCanalDeVenta,
                inicioTransaccionAltan ?? inicioTransaccionCanalDeVenta,
                finTransaccionAltan ?? DateTime.UtcNow,
                beId ?? "N/A",
                request.Msisdn,
                montoRecarga,
                offerId,
                request.ChannelOfSale,
                request.PipeOfSale,
                request.IdPoS ?? "N/A",
                "N/A",
                "ERROR_404",
                cancellationToken);
            
            return StatusCode(StatusCodes.Status404NotFound, new
            {
                error = "Endpoint incorrecto",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al comprar producto. MSISDN: {Msisdn}", request.Msisdn);
            
            await GuardarTransaccionEnBD(
                inicioTransaccionCanalDeVenta,
                inicioTransaccionAltan ?? inicioTransaccionCanalDeVenta,
                finTransaccionAltan ?? DateTime.UtcNow,
                beId ?? "N/A",
                request.Msisdn,
                montoRecarga,
                offerId,
                request.ChannelOfSale,
                request.PipeOfSale,
                request.IdPoS ?? "N/A",
                "N/A",
                "ERROR_500",
                cancellationToken);
            
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al comprar producto",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Guarda una transacción de recarga en la base de datos
    /// </summary>
    private async Task GuardarTransaccionEnBD(
        DateTime inicioTransaccionCanalDeVenta,
        DateTime inicioTransaccionAltan,
        DateTime? finTransaccionAltan,
        string be,
        string msisdn,
        decimal montoRecarga,
        string offerId,
        string canalDeVenta,
        string medio,
        string idPOS,
        string orderId,
        string resultadoTransaccion,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("=== MÉTODO GuardarTransaccionEnBD INVOCADO ===");
        try
        {
            _logger.LogInformation("=== INICIANDO GUARDADO DE TRANSACCIÓN ===");
            _logger.LogInformation("MSISDN: {Msisdn}, OrderId: {OrderId}, Monto: {Monto}, OfferId: {OfferId}", 
                msisdn, orderId, montoRecarga, offerId);
            _logger.LogInformation("BE: {BE}, CanalDeVenta: {CanalDeVenta}, Medio: {Medio}, IdPOS: {IdPOS}", 
                be, canalDeVenta, medio, idPOS);
            _logger.LogInformation("ResultadoTransaccion: {ResultadoTransaccion}", resultadoTransaccion);

            if (string.IsNullOrWhiteSpace(msisdn))
            {
                _logger.LogError("VALIDACIÓN FALLIDA: MSISDN está vacío. No se puede guardar la transacción.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(offerId))
            {
                _logger.LogError("VALIDACIÓN FALLIDA: OfferId está vacío. No se puede guardar la transacción.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(canalDeVenta))
            {
                _logger.LogError("VALIDACIÓN FALLIDA: CanalDeVenta está vacío. No se puede guardar la transacción.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(medio))
            {
                _logger.LogError("VALIDACIÓN FALLIDA: Medio está vacío. No se puede guardar la transacción.");
                return;
            }

            _logger.LogInformation("Todas las validaciones pasaron. Procediendo a crear objeto transacción...");

            var transaccion = new AltanTransaccionRecarga
            {
                InicioTransaccionCanalDeVenta = inicioTransaccionCanalDeVenta,
                InicioTransaccionAltan = inicioTransaccionAltan,
                FinTransaccionAltan = finTransaccionAltan,
                FinTransaccionCanalDeVenta = DateTime.UtcNow,
                BE = be,
                MSISDN = msisdn,
                MontoRecarga = montoRecarga,
                OfferId = offerId,
                CanalDeVenta = canalDeVenta,
                Medio = medio,
                IdPOS = idPOS ?? "N/A",
                OrderId = orderId ?? "N/A",
                ResultadoTransaccion = resultadoTransaccion ?? "N/A",
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Objeto transacción creado. Agregando al contexto DbContext...");
            _context.AltanTransaccionesRecargas.Add(transaccion);
            
            _logger.LogInformation("Guardando cambios en base de datos (SaveChangesAsync)...");
            var recordsAffected = await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("=== TRANSACCIÓN GUARDADA EXITOSAMENTE ===");
            _logger.LogInformation("Records afectados: {RecordsAffected}, IdTransaccion: {IdTransaccion}, OrderId: {OrderId}, Resultado: {Resultado}",
                recordsAffected, transaccion.IdTransaccion, orderId, resultadoTransaccion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== ERROR AL GUARDAR TRANSACCIÓN EN BASE DE DATOS ===");
            _logger.LogError("MSISDN: {Msisdn}, OrderId: {OrderId}", msisdn, orderId);
            _logger.LogError("Exception Type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("Exception Message: {ExceptionMessage}", ex.Message);
            _logger.LogError("Inner Exception: {InnerException}", ex.InnerException?.Message ?? "None");
            _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                _logger.LogError("Inner Exception Type: {InnerType}", ex.InnerException.GetType().Name);
                _logger.LogError("Inner Exception Message: {InnerMessage}", ex.InnerException.Message);
            }
        }
    }

    #endregion
}


