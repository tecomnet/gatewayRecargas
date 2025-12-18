using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.Sql;

namespace GateWayRecargas.Controllers
{
    [ApiController]
    [Route("/api/v1.0/[controller]")]
    [Tags("2. Altan - Recargas")]
    [Authorize] // Requiere autenticación JWT
    public class ProductController : ControllerBase
    {
        private readonly IAltanApiService _altanApiService;
        private readonly TecomNetDbContext _context;
        private readonly ILogger<ProductController> _logger;

        public ProductController(
            IAltanApiService altanApiService, 
            TecomNetDbContext context,
            ILogger<ProductController> logger)
        {
            _altanApiService = altanApiService;
            _context = context;
            _logger = logger;
        }

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
            // Timestamp de inicio de transacción en el canal de venta
            var inicioTransaccionCanalDeVenta = DateTime.UtcNow;
            DateTime? inicioTransaccionAltan = null;
            DateTime? finTransaccionAltan = null;
            
            string? beId = null;
            decimal montoRecarga = 0;
            string offerId = string.Empty;
            
            try
            {
                // Validar modelo
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

                // Normalizar valores que requieren formato específico
                request.ChannelOfSale = request.ChannelOfSale?.ToUpperInvariant() ?? "RETAILER";
                request.PipeOfSale = request.PipeOfSale?.ToUpperInvariant() ?? "GATEWAY_RECARGA";
                
                // Validar valores permitidos
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

                // Obtener OfferId (primera oferta de la lista)
                offerId = request.Offerings?.FirstOrDefault() ?? string.Empty;
                
                if (string.IsNullOrEmpty(offerId))
                {
                    _logger.LogWarning("No se proporcionó OfferId en la lista de ofertas. Offerings: {Offerings}",
                        string.Join(", ", request.Offerings ?? new List<string>()));
                }

                // Obtener monto de la oferta desde la base de datos
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

                // Intentar obtener BE del MSISDN (opcional - no bloquea la transacción si falla)
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

                // Intentar obtener el token del header Authorization si no viene en query
                string? accessToken = token?.Trim();
                
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
                
                // Asegurar que el token esté limpio antes de usarlo
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    accessToken = accessToken.Trim();
                }

                // Timestamp antes de llamar a Altan
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
                
                // Timestamp después de recibir respuesta de Altan
                finTransaccionAltan = DateTime.UtcNow;
                
                _logger.LogInformation("Compra exitosa. Order ID: {OrderId}, EffectiveDate: {EffectiveDate}", 
                    purchaseResponse.Order?.Id, purchaseResponse.EffectiveDate);
                
                // Guardar transacción exitosa en base de datos
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

                var finTransaccionCanalDeVenta = DateTime.UtcNow;
                
                return Ok(purchaseResponse);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Datos inválidos para compra. MSISDN: {Msisdn}", request?.Msisdn ?? "N/A");
                
                // Guardar transacción fallida
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
                
                // Guardar transacción fallida
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
                
                // Guardar transacción fallida
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
                
                // Guardar transacción fallida
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
                
                // Guardar transacción fallida
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

                // Validar campos requeridos antes de crear el objeto
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
                // No lanzamos la excepción para no interrumpir el flujo principal
                // Solo la logueamos con más detalle para debugging
                _logger.LogError(ex, "=== ERROR AL GUARDAR TRANSACCIÓN EN BASE DE DATOS ===");
                _logger.LogError("MSISDN: {Msisdn}, OrderId: {OrderId}", msisdn, orderId);
                _logger.LogError("Exception Type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception Message: {ExceptionMessage}", ex.Message);
                _logger.LogError("Inner Exception: {InnerException}", ex.InnerException?.Message ?? "None");
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
                
                // Si hay una excepción de Entity Framework, loguear más detalles
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception Type: {InnerType}", ex.InnerException.GetType().Name);
                    _logger.LogError("Inner Exception Message: {InnerMessage}", ex.InnerException.Message);
                }
            }
        }
    }
}

