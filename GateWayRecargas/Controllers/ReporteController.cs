using Microsoft.AspNetCore.Mvc;
using TecomNet.DomainService.Core.Services;

namespace GateWayRecargas.Controllers;

[ApiController]
[Route("/api/v1.0/[controller]")]
public class ReporteController : ControllerBase
{
    private readonly IReporteRecargasService _reporteRecargasService;
    private readonly ILogger<ReporteController> _logger;

    public ReporteController(
        IReporteRecargasService reporteRecargasService,
        ILogger<ReporteController> logger)
    {
        _reporteRecargasService = reporteRecargasService;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint de prueba para generar reporte manualmente
    /// </summary>
    /// <param name="fecha">Fecha del reporte (formato: YYYY-MM-DD). Si no se proporciona, usa la fecha de hoy</param>
    /// <param name="todas">Si es true, genera reporte con TODAS las transacciones disponibles (sin filtro de fecha). Útil para pruebas</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Información del archivo generado</returns>
    [HttpPost("generar-prueba")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GenerarReportePrueba(
        [FromQuery] string? fecha = null,
        [FromQuery] bool todas = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DateTime fechaReporte;
            
            if (string.IsNullOrWhiteSpace(fecha))
            {
                fechaReporte = DateTime.UtcNow.Date;
            }
            else
            {
                if (!DateTime.TryParse(fecha, out fechaReporte))
                {
                    return BadRequest(new
                    {
                        error = "Fecha inválida",
                        message = "El formato de fecha debe ser YYYY-MM-DD (ejemplo: 2025-12-05)"
                    });
                }
                fechaReporte = fechaReporte.Date;
            }

            if (todas)
            {
                _logger.LogInformation("Generando reporte de prueba con TODAS las transacciones disponibles (sin filtro de fecha)");
                var rutaArchivoTodas = await _reporteRecargasService.GenerarReporteLocalAsync(null, cancellationToken);
                
                if (string.IsNullOrEmpty(rutaArchivoTodas) || !System.IO.File.Exists(rutaArchivoTodas))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        error = "Error al generar reporte",
                        message = "No se pudo generar el archivo del reporte"
                    });
                }

                var fileInfoTodas = new System.IO.FileInfo(rutaArchivoTodas);
                var contenidoTodas = await System.IO.File.ReadAllTextAsync(rutaArchivoTodas, cancellationToken);
                var lineasTodas = contenidoTodas.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                return Ok(new
                {
                    mensaje = "Reporte generado exitosamente con TODAS las transacciones",
                    rutaArchivo = rutaArchivoTodas,
                    nombreArchivo = fileInfoTodas.Name,
                    tamanioBytes = fileInfoTodas.Length,
                    cantidadTransacciones = lineasTodas.Length,
                    contenido = contenidoTodas,
                    fechaGeneracion = DateTime.UtcNow
                });
            }

            _logger.LogInformation("Generando reporte de prueba para fecha: {Fecha:yyyy-MM-dd}", fechaReporte);

            // Solo generar localmente (no enviar al SFTP)
            var rutaArchivo = await _reporteRecargasService.GenerarReporteLocalAsync(fechaReporte, cancellationToken);

            if (string.IsNullOrEmpty(rutaArchivo) || !System.IO.File.Exists(rutaArchivo))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error al generar reporte",
                    message = "No se pudo generar el archivo del reporte"
                });
            }

            var fileInfo = new System.IO.FileInfo(rutaArchivo);
            var contenido = await System.IO.File.ReadAllTextAsync(rutaArchivo, cancellationToken);
            var lineas = contenido.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            return Ok(new
            {
                mensaje = "Reporte generado exitosamente",
                fecha = fechaReporte.ToString("yyyy-MM-dd"),
                rutaArchivo = rutaArchivo,
                nombreArchivo = fileInfo.Name,
                tamanioBytes = fileInfo.Length,
                cantidadTransacciones = lineas.Length,
                contenido = contenido,
                fechaGeneracion = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte de prueba");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al generar reporte",
                message = ex.Message
            });
        }
    }
}

