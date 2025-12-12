using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;

namespace GateWayRecargas.Controllers;

[ApiController]
[Route("/api/v1.0/[controller]")]
[AllowAnonymous] // Este endpoint no requiere autenticación
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
}

