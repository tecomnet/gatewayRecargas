using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using TecomNet.DomainService.Core.Services;

namespace TecomNet.Infrastructure.WebServices;

public class SftpClientService : ISftpClientService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SftpClientService> _logger;
    private readonly string _host;
    private readonly int _puerto;
    private readonly string _usuario;
    private readonly string _password;
    private readonly string _directorioDestino;
    private SftpClient? _sftpClient;

    public SftpClientService(IConfiguration configuration, ILogger<SftpClientService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        _host = _configuration["SFTP:Host"] ?? throw new InvalidOperationException("SFTP:Host no configurado");
        _puerto = int.Parse(_configuration["SFTP:Puerto"] ?? "22");
        _usuario = _configuration["SFTP:Usuario"] ?? throw new InvalidOperationException("SFTP:Usuario no configurado");
        _password = _configuration["SFTP:Password"] ?? throw new InvalidOperationException("SFTP:Password no configurado");
        _directorioDestino = _configuration["SFTP:DirectorioDestino"] ?? throw new InvalidOperationException("SFTP:DirectorioDestino no configurado");
    }

    private SftpClient ObtenerClienteSftp()
    {
        if (_sftpClient?.IsConnected == true)
        {
            return _sftpClient;
        }

        _logger.LogInformation("Conectando al SFTP: {Host}:{Puerto}, Usuario: {Usuario}", _host, _puerto, _usuario);
        
        var connectionInfo = new ConnectionInfo(_host, _puerto, _usuario,
            new PasswordAuthenticationMethod(_usuario, _password));

        _sftpClient = new SftpClient(connectionInfo);
        _sftpClient.Connect();

        _logger.LogInformation("Conexión SFTP establecida exitosamente");
        return _sftpClient;
    }

    public Task<bool> VerificarConexionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = ObtenerClienteSftp();
            client.ListDirectory(_directorioDestino);
            _logger.LogInformation("Verificación de conexión SFTP exitosa");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar conexión SFTP");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> EnviarArchivoAsync(string rutaArchivoLocal, string nombreArchivoRemoto, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(rutaArchivoLocal))
            {
                _logger.LogError("El archivo local no existe: {RutaArchivoLocal}", rutaArchivoLocal);
                return false;
            }

            var client = ObtenerClienteSftp();
            
            // Asegurar que el directorio destino existe
            if (!client.Exists(_directorioDestino))
            {
                _logger.LogInformation("Creando directorio destino: {DirectorioDestino}", _directorioDestino);
                client.CreateDirectory(_directorioDestino);
            }

            // Ruta completa del archivo remoto
            var rutaArchivoRemoto = $"{_directorioDestino.TrimEnd('/')}/{nombreArchivoRemoto}";

            _logger.LogInformation("Enviando archivo al SFTP: {ArchivoLocal} -> {ArchivoRemoto}", 
                rutaArchivoLocal, rutaArchivoRemoto);

            // Leer archivo local y subirlo
            using (var fileStream = File.OpenRead(rutaArchivoLocal))
            {
                await Task.Run(() => client.UploadFile(fileStream, rutaArchivoRemoto, true), cancellationToken);
            }

            _logger.LogInformation("Archivo enviado exitosamente al SFTP: {ArchivoRemoto}", rutaArchivoRemoto);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar archivo al SFTP. Archivo: {RutaArchivoLocal}", rutaArchivoLocal);
            return false;
        }
    }

    public void Dispose()
    {
        if (_sftpClient?.IsConnected == true)
        {
            _sftpClient.Disconnect();
        }
        _sftpClient?.Dispose();
    }
}

