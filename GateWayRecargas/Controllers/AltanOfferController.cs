using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.ComponentModel.DataAnnotations;
using TecomNet.Domain.Models;
using TecomNet.Infrastructure.Sql;

namespace GateWayRecargas.Controllers
{
    [ApiController]
    [Route("/api/v1.0/[controller]")]
    public class AltanOfferController : ControllerBase
    {
        private readonly TecomNetDbContext _context;
        private readonly ILogger<AltanOfferController> _logger;

        public AltanOfferController(TecomNetDbContext context, ILogger<AltanOfferController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene las ofertas de Altan filtradas por BeId
        /// </summary>
        /// <param name="beId">Identificador BE (Business Entity ID)</param>
        /// <param name="cancellationToken">Token de cancelación</param>
        /// <returns>Lista de ofertas (CommercialName, IDOffer, Price, MvnoId)</returns>
        [HttpGet("byBeId")]
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

                // Consulta SQL optimizada usando JOIN en lugar de subconsulta
                // La tabla dbo.Mvnos contiene BeId y se relaciona con dbo.AltanOffers mediante MvnoId (Foreign Key)
                var startTime = DateTime.UtcNow;
                
                // Usar FormattableString para parametrización segura
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
    }
}

