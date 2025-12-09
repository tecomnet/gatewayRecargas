namespace TecomNet.DomainService.Core.Services;

public interface IReporteRecargasService
{
    Task<bool> GenerarYEnviarReporteDiarioAsync(DateTime fechaReporte, CancellationToken cancellationToken = default);
    Task<string> GenerarReporteLocalAsync(DateTime? fechaReporte, CancellationToken cancellationToken = default);
}

