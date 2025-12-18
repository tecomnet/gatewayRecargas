using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TecomNet.Domain.Models;
using TecomNet.Infrastructure.Sql;

namespace GateWayRecargas.Controllers;

/// <summary>
/// Controlador temporal para crear usuarios de prueba
/// ELIMINAR o proteger en producción
/// </summary>
[ApiController]
[Route("/api/v1.0/[controller]")]
[Tags("1. Autenticación JWT")]
[AllowAnonymous] // Temporal - eliminar en producción
public class SetupController : ControllerBase
{
    private readonly TecomNetDbContext _context;
    private readonly ILogger<SetupController> _logger;

    public SetupController(TecomNetDbContext context, ILogger<SetupController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("update-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null)
            {
                return NotFound(new { error = "Usuario no encontrado" });
            }

            // Generar nuevo hash del password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            user.PasswordHash = passwordHash;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password actualizado para usuario: {Username}", request.Username);

            return Ok(new
            {
                message = "Password actualizado exitosamente",
                username = user.Username
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar password");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al actualizar password",
                message = ex.Message
            });
        }
    }

}

public class UpdatePasswordRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}

