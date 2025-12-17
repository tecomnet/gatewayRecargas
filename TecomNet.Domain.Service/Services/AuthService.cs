using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;
using TecomNet.Infrastructure.Sql;

namespace TecomNet.Domain.Service.Services;

public class AuthService : IAuthService
{
    private readonly TecomNetDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        TecomNetDbContext context,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            _logger.LogWarning("Intento de login con usuario inexistente: {Username}", request.Username);
            return null;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Intento de login con usuario inactivo: {Username}", request.Username);
            return null;
        }

        // Verificar password (BCrypt)
        try
        {
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Password incorrecto para usuario: {Username}", request.Username);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar password para usuario: {Username}. Hash inválido o corrupto.", request.Username);
            throw new InvalidOperationException("El hash de la contraseña en la base de datos no es válido. Por favor, actualiza la contraseña del usuario.", ex);
        }

        // Actualizar último login
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Generar token
        var token = GenerateToken(user);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(1), // Token válido por 1 hora
            Username = user.Username
        };
    }

    public string GenerateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey no configurado");
        var issuer = jwtSettings["Issuer"] ?? "GateWayRecargas";
        var audience = jwtSettings["Audience"] ?? "GateWayRecargas-Users";
        var expirationMinutes = int.Parse(jwtSettings["ExpirationInMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            _logger.LogWarning("Intento de cambio de contraseña para usuario inexistente: {UserId}", userId);
            return false;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Intento de cambio de contraseña para usuario inactivo: {UserId}", userId);
            return false;
        }

        // Verificar contraseña actual
        try
        {
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Contraseña actual incorrecta para usuario: {Username} (Id: {UserId})", user.Username, userId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar contraseña actual para usuario: {Username} (Id: {UserId})", user.Username, userId);
            throw new InvalidOperationException("El hash de la contraseña en la base de datos no es válido.", ex);
        }

        // Validar que la nueva contraseña no sea igual a la actual
        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        {
            _logger.LogWarning("La nueva contraseña no puede ser igual a la contraseña actual. Usuario: {Username} (Id: {UserId})", user.Username, userId);
            return false;
        }

        // Validar fortaleza de la nueva contraseña
        var validationResult = ValidatePasswordStrength(request.NewPassword);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Nueva contraseña no cumple con los requisitos de seguridad. Usuario: {Username} (Id: {UserId}), Errores: {Errors}", 
                user.Username, userId, string.Join(", ", validationResult.Errors));
            return false;
        }

        // Generar nuevo hash y actualizar
        try
        {
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordHash = newPasswordHash;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Contraseña actualizada exitosamente para usuario: {Username} (Id: {UserId})", user.Username, userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar contraseña en base de datos para usuario: {Username} (Id: {UserId})", user.Username, userId);
            throw;
        }
    }

    private (bool IsValid, List<string> Errors) ValidatePasswordStrength(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("La contraseña no puede estar vacía");
            return (false, errors);
        }

        if (password.Length < 8)
        {
            errors.Add("La contraseña debe tener al menos 8 caracteres");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("La contraseña debe contener al menos una letra mayúscula");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("La contraseña debe contener al menos una letra minúscula");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("La contraseña debe contener al menos un número");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            errors.Add("La contraseña debe contener al menos un carácter especial");
        }

        // Lista de contraseñas comunes/debiles (puedes expandir esta lista)
        var commonPasswords = new[]
        {
            "password", "12345678", "password123", "admin123", "123456789",
            "qwerty123", "welcome123", "letmein123", "monkey123", "dragon123"
        };

        if (commonPasswords.Any(common => password.Equals(common, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("La contraseña es demasiado común y no es segura");
        }

        return (errors.Count == 0, errors);
    }
}

