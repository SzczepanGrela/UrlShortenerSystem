namespace UrlShortener.API.Services;

public interface ILinkCleanupService
{
    Task PerformCleanupAsync(int retentionDays, CancellationToken cancellationToken = default);
}