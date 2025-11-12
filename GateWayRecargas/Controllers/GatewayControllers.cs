using Microsoft.AspNetCore.Mvc;
using TecomNet.DomainService.Core;

namespace GateWayRecargas.Controllers
{
    [ApiController]
    [Route("/api/v1.0/[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly IAltanApiService _altanApiService;
        private readonly ILogger<TokenController> _logger;

        public TokenController(IAltanApiService altanApiService, ILogger<TokenController> logger)
        {
            _altanApiService = altanApiService;
            _logger = logger;
        }

    }
    }


