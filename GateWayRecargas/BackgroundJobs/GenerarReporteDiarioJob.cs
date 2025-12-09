using Hangfire;
using TecomNet.DomainService.Core.Services;

namespace GateWayRecargas.BackgroundJobs;

public class GenerarReporteDiarioJob
{
    private readonly IReporteRecargasService _reporteRecargasService;
    private readonly ILogger<GenerarReporteDiarioJob> _logger;

    public GenerarReporteDiarioJob(
        IReporteRecargasService reporteRecargasService,
        ILogger<GenerarReporteDiarioJob> logger)
    {
        _reporteRecargasService = reporteRecargasService;
        _logger = logger;
    }

    /// <summary>
    /// Job que se ejecuta diariamente para generar el reporte del día anterior
    /// </summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("=== INICIANDO JOB DE GENERACIÓN DE REPORTE DIARIO ===");
            
            // Obtener fecha del día anterior
            var fechaAyer = DateTime.UtcNow.Date.AddDays(-1);
            
            _logger.LogInformation("Generando reporte para fecha: {Fecha:yyyy-MM-dd}", fechaAyer);

            var resultado = await _reporteRecargasService.GenerarYEnviarReporteDiarioAsync(fechaAyer, cancellationToken);

            if (resultado)
            {
                _logger.LogInformation("=== JOB COMPLETADO EXITOSAMENTE ===");
                _logger.LogInformation("Reporte generado para fecha: {Fecha:yyyy-MM-dd} (solo local - SFTP deshabilitado)", fechaAyer);
            }
            else
            {
                _logger.LogError("=== JOB COMPLETADO CON ERRORES ===");
                _logger.LogError("No se pudo generar el reporte para fecha: {Fecha:yyyy-MM-dd}", fechaAyer);
                throw new Exception($"Error al generar reporte para fecha: {fechaAyer:yyyy-MM-dd}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el job de generación de reporte diario");
            throw; // Hangfire reintentará según la configuración de AutomaticRetry
        }
    }
}

