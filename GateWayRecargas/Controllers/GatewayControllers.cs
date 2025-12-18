using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecomNet.Domain.Models;
using TecomNet.DomainService.Core.Services;

namespace GateWayRecargas.Controllers
{
    [ApiController]
    [Route("/api/v1.0/[controller]")]
    [Tags("2. Altan - Token")]
    [Authorize] // Requiere autenticación JWT
    public class TokenController : ControllerBase
    {
        private readonly IAltanApiService _altanApiService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(IAltanApiService altanApiService, ILogger<TokenController> logger)
        {
            _altanApiService = altanApiService;
            _logger = logger;
        }
        /// <param name="cancellationToken"

        [HttpPost]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<TokenResponse>> GetToken(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Solicitando token de acceso");
                var tokenResponse = await _altanApiService.GetAccessTokenAsync(cancellationToken);
                return Ok(tokenResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener token de acceso");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error al obtener token de acceso",
                    message = ex.Message
                });
            }
        }
    }
}


