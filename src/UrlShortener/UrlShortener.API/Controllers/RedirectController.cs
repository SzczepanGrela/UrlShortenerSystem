using Microsoft.AspNetCore.Mvc;
using UrlShortener.API.Services;

namespace UrlShortener.API.Controllers
{
    [ApiController]
    [Route("")]
    public class RedirectController : ControllerBase
    {
        private readonly ILinkService _linkService;
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<RedirectController> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public RedirectController(ILinkService linkService, IAnalyticsService analyticsService, ILogger<RedirectController> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _linkService = linkService;
            _analyticsService = analyticsService;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// Przekierowuje na oryginalny URL i rejestruje kliknięcie
        /// </summary>
        /// <param name="shortCode">Krótki kod</param>
        /// <returns>Przekierowanie</returns>
        [HttpGet("{shortCode}")]
        public async Task<IActionResult> RedirectToOriginal(string shortCode)
        {
            var link = await _linkService.GetLinkByShortCode(shortCode);

            if (link == null)
            {
                return NotFound("Link not found");
            }

            // Sprawdź wygaśnięcie
            if (link.ExpirationDate.HasValue && link.ExpirationDate.Value < DateTime.UtcNow)
            {
                return StatusCode(410, "Link has expired"); // 410 Gone
            }

            // Wyciągnij dane z HttpContext przed fire-and-forget
            var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() 
                ?? HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault() 
                ?? HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            var referer = HttpContext.Request.Headers["Referer"].ToString();

            // Zarejestruj kliknięcie w serwisie analityki (fire-and-forget)
            _ = Task.Factory.StartNew(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                    await analyticsService.RegisterClick(link.Id, link.OriginalUrl, ipAddress, userAgent, referer);
                }
                catch (Exception ex)
                {
                    // Log but don't fail the redirect
                    _logger.LogWarning(ex, "Analytics registration failed for link {LinkId}", link.Id);
                }
            }, TaskCreationOptions.LongRunning).Unwrap();

            return Redirect(link.OriginalUrl);
        }
    }
}