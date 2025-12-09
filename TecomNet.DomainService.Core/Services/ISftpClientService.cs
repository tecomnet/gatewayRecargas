namespace TecomNet.DomainService.Core.Services;

public interface ISftpClientService
{
    Task<bool> EnviarArchivoAsync(string rutaArchivoLocal, string nombreArchivoRemoto, CancellationToken cancellationToken = default);
    Task<bool> VerificarConexionAsync(CancellationToken cancellationToken = default);
}



