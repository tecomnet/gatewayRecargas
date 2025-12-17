using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;

namespace GateWayRecargas.Controllers;

[ApiController]
[Route("/api/v1.0/[controller]")]
[Tags("1. Autenticación JWT")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous] // Este endpoint no requiere autenticación
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            
            if (response == null)
            {
                return Unauthorized(new
                {
                    error = "Credenciales inválidas",
                    message = "Usuario o contraseña incorrectos"
                });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al realizar login");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al procesar la solicitud",
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Cambia la contraseña del usuario autenticado
    /// </summary>
    /// <param name="request">Datos del cambio de contraseña:
    /// - currentPassword: Contraseña actual del usuario
    /// - newPassword: Nueva contraseña (mínimo 8 caracteres, debe incluir mayúsculas, minúsculas, números y caracteres especiales)
    /// - confirmPassword: Confirmación de la nueva contraseña (debe coincidir con newPassword)
    /// </param>
    /// <returns>Resultado del cambio de contraseña</returns>
    [HttpPost("change-password")]
    [Authorize] // Requiere autenticación JWT
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            // Validar modelo
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    error = "Datos inválidos",
                    message = "La solicitud contiene datos inválidos",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            // Obtener el ID del usuario desde el token JWT
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("No se pudo obtener el ID del usuario desde el token JWT");
                return Unauthorized(new
                {
                    error = "Token inválido",
                    message = "No se pudo identificar al usuario"
                });
            }

            // Intentar cambiar la contraseña
            var result = await _authService.ChangePasswordAsync(userId, request);

            if (!result)
            {
                return BadRequest(new
                {
                    error = "No se pudo cambiar la contraseña",
                    message = "La contraseña actual es incorrecta, la nueva contraseña no cumple con los requisitos de seguridad, o la nueva contraseña es igual a la actual"
                });
            }

            return Ok(new
            {
                message = "Contraseña actualizada exitosamente"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error al cambiar contraseña - operación inválida");
            return BadRequest(new
            {
                error = "Error al cambiar contraseña",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cambiar contraseña");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al procesar la solicitud",
                message = "Ocurrió un error al cambiar la contraseña. Por favor, intente nuevamente."
            });
        }
    }
}

