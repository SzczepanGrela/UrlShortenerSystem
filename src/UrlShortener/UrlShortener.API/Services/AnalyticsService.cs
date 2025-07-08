using System.Text.Json;

namespace UrlShortener.API.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsService(HttpClient httpClient, IConfiguration configuration, ILogger<AnalyticsService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task RegisterClick(Guid linkId, string originalUrl, string? ipAddress, string? userAgent, string? referer)
        {
            _logger.LogInformation("Registering click for link: {LinkId}", linkId);
            
            try
            {
                var analyticsBaseUrl = _configuration["AnalyticsService:BaseUrl"] ?? "https://localhost:7001";

                var clickData = new
                {
                    LinkId = linkId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Referer = referer
                };

                var json = JsonSerializer.Serialize(clickData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{analyticsBaseUrl}/api/analytics/clicks", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Click registered successfully for link: {LinkId} ({OriginalUrl})", linkId, originalUrl);
                }
                else
                {
                    _logger.LogWarning("Failed to register click for link: {LinkId}, Status: {StatusCode}", 
                        linkId, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering click for link: {LinkId}", linkId);
            }
        }
    }
}