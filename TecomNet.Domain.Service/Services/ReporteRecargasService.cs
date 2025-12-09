using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.Sql;

namespace TecomNet.Domain.Service.Services;

public class ReporteRecargasService : IReporteRecargasService
{
    private readonly TecomNetDbContext _context;
    private readonly ISftpClientService _sftpClientService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReporteRecargasService> _logger;
    private readonly string _proveedorId;
    private readonly string _rutaTemporal;

    public ReporteRecargasService(
        TecomNetDbContext context,
        ISftpClientService sftpClientService,
        IConfiguration configuration,
        ILogger<ReporteRecargasService> logger)
    {
        _context = context;
        _sftpClientService = sftpClientService;
        _configuration = configuration;
        _logger = logger;
        
        _proveedorId = _configuration["ReporteRecargas:ProveedorId"] ?? "TECOMNET";
        _rutaTemporal = _configuration["ReporteRecargas:RutaTemporal"] ?? Path.Combine(Path.GetTempPath(), "ReportesRecargas");
        
        // Crear directorio temporal si no existe
        if (!Directory.Exists(_rutaTemporal))
        {
            Directory.CreateDirectory(_rutaTemporal);
        }
    }

    public async Task<bool> GenerarYEnviarReporteDiarioAsync(DateTime fechaReporte, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("=== INICIANDO GENERACIÓN DE REPORTE DIARIO ===");
            _logger.LogInformation("Fecha del reporte: {FechaReporte:yyyy-MM-dd}", fechaReporte);

            // Generar archivo local
            var rutaArchivoLocal = await GenerarReporteLocalAsync(fechaReporte, cancellationToken);
            
            if (string.IsNullOrEmpty(rutaArchivoLocal) || !File.Exists(rutaArchivoLocal))
            {
                _logger.LogError("No se pudo generar el archivo del reporte");
                return false;
            }

            // ===========================================
            // SFTP TEMPORALMENTE DESHABILITADO
            // Esperando confirmación de credenciales y acceso
            // ===========================================
            _logger.LogWarning("=== ENVÍO AL SFTP DESHABILITADO TEMPORALMENTE ===");
            _logger.LogInformation("Archivo generado localmente: {RutaArchivo}", rutaArchivoLocal);
            _logger.LogInformation("El archivo se mantiene en la carpeta local para revisión");
            
            // Código comentado - será reactivado cuando se confirme el acceso SFTP
            /*
            // Nombre del archivo para SFTP
            var nombreArchivoRemoto = $"gw_rec_tecomnet_{fechaReporte:yyyyMMdd}.txt";

            // Enviar al SFTP
            _logger.LogInformation("Enviando reporte al SFTP: {NombreArchivo}", nombreArchivoRemoto);
            var enviado = await _sftpClientService.EnviarArchivoAsync(rutaArchivoLocal, nombreArchivoRemoto, cancellationToken);

            if (enviado)
            {
                _logger.LogInformation("=== REPORTE GENERADO Y ENVIADO EXITOSAMENTE ===");
                _logger.LogInformation("Archivo: {NombreArchivo}", nombreArchivoRemoto);
                
                // Opcional: Eliminar archivo temporal después de enviar
                try
                {
                    File.Delete(rutaArchivoLocal);
                    _logger.LogInformation("Archivo temporal eliminado: {RutaArchivo}", rutaArchivoLocal);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo eliminar el archivo temporal: {RutaArchivo}", rutaArchivoLocal);
                }
            }
            else
            {
                _logger.LogError("=== ERROR AL ENVIAR REPORTE AL SFTP ===");
            }

            return enviado;
            */

            _logger.LogInformation("=== REPORTE GENERADO EXITOSAMENTE (SOLO LOCAL) ===");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte diario para fecha: {FechaReporte}", fechaReporte);
            return false;
        }
    }

    public async Task<string> GenerarReporteLocalAsync(DateTime? fechaReporte, CancellationToken cancellationToken = default)
    {
        try
        {
            List<AltanTransaccionRecarga> transacciones;
            DateTime fechaParaNombre;
            string nombreArchivo;

            if (fechaReporte.HasValue)
            {
                _logger.LogInformation("Generando reporte local para fecha: {FechaReporte:yyyy-MM-dd}", fechaReporte.Value);
                
                // Obtener todas las transacciones del día
                var fechaInicio = fechaReporte.Value.Date;
                var fechaFin = fechaInicio.AddDays(1);

                _logger.LogInformation("Buscando transacciones entre {FechaInicio:yyyy-MM-dd HH:mm:ss} UTC y {FechaFin:yyyy-MM-dd HH:mm:ss} UTC", 
                    fechaInicio, fechaFin);

                transacciones = await _context.AltanTransaccionesRecargas
                    .Where(t => t.CreatedAt >= fechaInicio && t.CreatedAt < fechaFin)
                    .OrderBy(t => t.CreatedAt)
                    .ToListAsync(cancellationToken);

                fechaParaNombre = fechaReporte.Value;
                nombreArchivo = $"gw_rec_tecomnet_{fechaParaNombre:yyyyMMdd}.txt";
                
                _logger.LogInformation("Transacciones encontradas para el reporte: {Count}", transacciones.Count);
            }
            else
            {
                _logger.LogInformation("Generando reporte local con TODAS las transacciones disponibles (sin filtro de fecha)");
                
                // Obtener TODAS las transacciones (útil para pruebas)
                transacciones = await _context.AltanTransaccionesRecargas
                    .OrderBy(t => t.CreatedAt)
                    .ToListAsync(cancellationToken);

                fechaParaNombre = DateTime.UtcNow.Date;
                nombreArchivo = $"gw_rec_tecomnet_TODAS_{fechaParaNombre:yyyyMMdd}.txt";
                
                _logger.LogInformation("Total de transacciones encontradas: {Count}", transacciones.Count);
                
                if (transacciones.Count > 0)
                {
                    var primeraTransaccion = transacciones.First();
                    var ultimaTransaccion = transacciones.Last();
                    _logger.LogInformation("Rango de fechas: {FechaInicio:yyyy-MM-dd HH:mm:ss} UTC a {FechaFin:yyyy-MM-dd HH:mm:ss} UTC", 
                        primeraTransaccion.CreatedAt, ultimaTransaccion.CreatedAt);
                }
            }

            if (transacciones.Count == 0)
            {
                _logger.LogWarning("No se encontraron transacciones. Se generará un archivo vacío.");
            }

            // Nombre del archivo
            var rutaArchivo = Path.Combine(_rutaTemporal, nombreArchivo);

            // Generar contenido del archivo
            using (var writer = new StreamWriter(rutaArchivo, false, System.Text.Encoding.UTF8))
            {
                // Formatear cada transacción según el layout del PDF
                foreach (var transaccion in transacciones)
                {
                    var linea = FormatearLineaTransaccion(transaccion);
                    await writer.WriteLineAsync(linea);
                }
            }

            _logger.LogInformation("Archivo de reporte generado: {RutaArchivo}", rutaArchivo);
            return rutaArchivo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar reporte local para fecha: {FechaReporte}", fechaReporte);
            return string.Empty;
        }
    }

    private string FormatearLineaTransaccion(AltanTransaccionRecarga transaccion)
    {
        // Formato delimitado por pipe (|) según el layout del PDF
        // Orden de campos según el PDF:
        var campos = new List<string>
        {
            FormatearFecha(transaccion.InicioTransaccionCanalDeVenta),
            FormatearFecha(transaccion.InicioTransaccionAltan),
            FormatearFecha(transaccion.FinTransaccionAltan),
            FormatearFecha(transaccion.FinTransaccionCanalDeVenta),
            transaccion.BE ?? "",
            transaccion.MSISDN ?? "",
            transaccion.MontoRecarga.ToString("F2"),
            transaccion.OfferId ?? "",
            transaccion.CanalDeVenta ?? "",
            transaccion.Medio ?? "",
            transaccion.IdPOS ?? "",
            transaccion.OrderId ?? "",
            transaccion.ResultadoTransaccion ?? "",
            FormatearAplicaReverso(transaccion.AplicaReverso),
            FormatearFecha(transaccion.InicioTransaccionReversa),
            FormatearFecha(transaccion.InicioTransaccionReversaAltan),
            FormatearFecha(transaccion.FinTransaccionReversaAltan),
            FormatearFecha(transaccion.FinTransaccionReversa),
            transaccion.OrderIdReversa ?? "",
            FormatearResultadoReversa(transaccion.ResultadoTransaccionReversa)
        };

        return string.Join("|", campos);
    }

    private string FormatearFecha(DateTime? fecha)
    {
        if (!fecha.HasValue)
            return "";

        // Formato: yyyyMMddHHmmss según el PDF (ejemplo: 20251205161450)
        return fecha.Value.ToString("yyyyMMddHHmmss");
    }

    private string FormatearAplicaReverso(bool? aplicaReverso)
    {
        if (!aplicaReverso.HasValue)
            return "";

        return aplicaReverso.Value ? "SI" : "NO";
    }

    private string FormatearResultadoReversa(string? resultado)
    {
        if (string.IsNullOrWhiteSpace(resultado))
            return "";

        // Normalizar valores según PDF: Exitosa, Fallida
        return resultado.ToUpperInvariant();
    }
}

