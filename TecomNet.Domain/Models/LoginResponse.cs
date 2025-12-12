namespace TecomNet.Domain.Models;

public class LoginResponse
{
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public string Username { get; set; } = null!;
}

