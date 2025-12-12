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

    [HttpPost("create-test-user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateTestUser([FromBody] CreateUserRequest request)
    {
        try
        {
            // Verificar si el usuario ya existe
            var exists = await _context.Users
                .AnyAsync(u => u.Username == request.Username || u.Email == request.Email);

            if (exists)
            {
                return BadRequest(new { error = "El usuario o email ya existe" });
            }

            // Generar hash del password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BCrypt.Net.BCrypt.GenerateSalt(11));

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                PasswordHash = passwordHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario de prueba creado: {Username}", request.Username);

            return Ok(new
            {
                message = "Usuario creado exitosamente",
                username = user.Username,
                email = user.Email
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear usuario de prueba");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Error al crear usuario",
                message = ex.Message
            });
        }
    }
}

public class CreateUserRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

