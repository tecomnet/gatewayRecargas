using Hangfire.Dashboard;

namespace GateWayRecargas;

/// <summary>
/// Filtro de autorización simple para Hangfire Dashboard
/// En producción, deberías implementar autenticación real
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // En desarrollo, permitir acceso a todos
        // En producción, implementar autenticación real
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return environment == "Development" || true; // Cambiar esto en producción
    }
}








