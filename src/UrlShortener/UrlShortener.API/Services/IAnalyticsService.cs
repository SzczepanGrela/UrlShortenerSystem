namespace UrlShortener.API.Services
{
    public interface IAnalyticsService
    {
        Task RegisterClick(Guid linkId, string originalUrl, string? ipAddress, string? userAgent, string? referer);
    }
}