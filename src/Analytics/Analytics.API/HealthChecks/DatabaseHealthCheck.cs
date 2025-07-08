using Microsoft.Extensions.Diagnostics.HealthChecks;
using Analytics.Storage;
using Microsoft.EntityFrameworkCore;

namespace Analytics.API.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AnalyticsDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(AnalyticsDbContext context, ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Database.CanConnectAsync(cancellationToken);
            
            var pendingMigrationsCount = (await _context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            
            if (pendingMigrationsCount > 0)
            {
                _logger.LogWarning("Database has {PendingMigrations} pending migrations", pendingMigrationsCount);
                return HealthCheckResult.Degraded($"Database has {pendingMigrationsCount} pending migrations");
            }

            return HealthCheckResult.Healthy("Database connection is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}