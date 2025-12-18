using System.ComponentModel.DataAnnotations;

namespace TecomNet.Domain.Models;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "La contraseña actual es requerida")]
    public string CurrentPassword { get; set; } = null!;

    [Required(ErrorMessage = "La nueva contraseña es requerida")]
    [MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "La confirmación de contraseña es requerida")]
    [Compare("NewPassword", ErrorMessage = "La nueva contraseña y la confirmación no coinciden")]
    public string ConfirmPassword { get; set; } = null!;
}
