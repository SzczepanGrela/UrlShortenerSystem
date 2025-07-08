using Microsoft.Extensions.Diagnostics.HealthChecks;
using Analytics.API.Services;

namespace Analytics.API.HealthChecks;

public class GeoLocationHealthCheck : IHealthCheck
{
    private readonly GeoLocationService _geoLocationService;
    private readonly ILogger<GeoLocationHealthCheck> _logger;

    public GeoLocationHealthCheck(GeoLocationService geoLocationService, ILogger<GeoLocationHealthCheck> logger)
    {
        _geoLocationService = geoLocationService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Test with a known IP address (Google's public DNS)
            var result = await _geoLocationService.GetLocationAsync("8.8.8.8");
            
            if (result.Country != null || result.City != null)
            {
                return HealthCheckResult.Healthy("GeoLocation service is healthy");
            }
            
            return HealthCheckResult.Degraded("GeoLocation service returned null result");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeoLocation health check failed");
            return HealthCheckResult.Unhealthy("GeoLocation service failed", ex);
        }
    }
}