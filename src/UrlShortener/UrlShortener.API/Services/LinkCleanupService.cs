using Microsoft.EntityFrameworkCore;
using UrlShortener.Storage;

namespace UrlShortener.API.Services;

public class LinkCleanupService : BackgroundService, ILinkCleanupService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LinkCleanupService> _logger;
    private readonly IConfiguration _configuration;

    public LinkCleanupService(
        IServiceProvider serviceProvider,
        ILogger<LinkCleanupService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanupInterval = _configuration.GetValue<int>("LinkCleanup:IntervalHours", 6);
        var retentionDays = _configuration.GetValue<int>("LinkCleanup:RetentionDays", 30);

        _logger.LogInformation("Link cleanup service started. Interval: {IntervalHours}h, Retention: {RetentionDays} days", 
            cleanupInterval, retentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(retentionDays, stoppingToken);
                
                var nextRun = DateTime.UtcNow.AddHours(cleanupInterval);
                _logger.LogInformation("Next cleanup scheduled for: {NextRun}", nextRun);
                
                await Task.Delay(TimeSpan.FromHours(cleanupInterval), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Link cleanup service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during link cleanup");
                
                // Wait 1 hour before retry on error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    public async Task PerformCleanupAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UrlShortenerDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        
        _logger.LogInformation("Starting cleanup of expired links older than {CutoffDate}", cutoffDate);

        // Clean up expired links
        var expiredLinksQuery = context.Links
            .Where(l => l.ExpirationDate.HasValue && l.ExpirationDate.Value < DateTime.UtcNow);

        var expiredCount = await expiredLinksQuery.CountAsync(cancellationToken);
        
        if (expiredCount > 0)
        {
            _logger.LogInformation("Found {ExpiredCount} expired links to clean up", expiredCount);
            
            // Remove expired links in batches to avoid memory issues
            var batchSize = 1000;
            var processedCount = 0;
            
            while (processedCount < expiredCount)
            {
                var batch = await expiredLinksQuery
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
                
                if (!batch.Any())
                    break;
                
                context.Links.RemoveRange(batch);
                await context.SaveChangesAsync(cancellationToken);
                
                processedCount += batch.Count;
                _logger.LogInformation("Cleaned up {ProcessedCount}/{ExpiredCount} expired links", 
                    processedCount, expiredCount);
            }
        }

        // Clean up old inactive links (soft deleted)
        var inactiveLinksQuery = context.Links
            .Where(l => !l.IsActive && l.CreationDate < cutoffDate);

        var inactiveCount = await inactiveLinksQuery.CountAsync(cancellationToken);
        
        if (inactiveCount > 0)
        {
            _logger.LogInformation("Found {InactiveCount} old inactive links to clean up", inactiveCount);
            
            var batchSize = 1000;
            var processedCount = 0;
            
            while (processedCount < inactiveCount)
            {
                var batch = await inactiveLinksQuery
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
                
                if (!batch.Any())
                    break;
                
                context.Links.RemoveRange(batch);
                await context.SaveChangesAsync(cancellationToken);
                
                processedCount += batch.Count;
                _logger.LogInformation("Cleaned up {ProcessedCount}/{InactiveCount} old inactive links", 
                    processedCount, inactiveCount);
            }
        }

        var totalCleaned = expiredCount + inactiveCount;
        if (totalCleaned > 0)
        {
            _logger.LogInformation("Cleanup completed successfully. Total links removed: {TotalCleaned}", totalCleaned);
        }
        else
        {
            _logger.LogInformation("No links required cleanup");
        }
    }
}