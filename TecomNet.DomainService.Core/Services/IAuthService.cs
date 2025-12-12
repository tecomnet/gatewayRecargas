using TecomNet.Domain.Models;

namespace TecomNet.DomainService.Core.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    string GenerateToken(User user);
}

