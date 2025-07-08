using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Storage;

namespace UrlShortener.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaintenanceController : ControllerBase
{
    private readonly UrlShortenerDbContext _context;
    private readonly ILogger<MaintenanceController> _logger;
    private readonly IConfiguration _configuration;

    public MaintenanceController(
        UrlShortenerDbContext context,
        ILogger<MaintenanceController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    [HttpPost("cleanup")]
    public async Task<IActionResult> ManualCleanup()
    {
        try
        {
            var retentionDays = _configuration.GetValue<int>("LinkCleanup:RetentionDays", 30);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            _logger.LogInformation("Manual cleanup initiated. Cutoff date: {CutoffDate}", cutoffDate);

            // Clean up expired links
            var expiredLinks = await _context.Links
                .Where(l => l.ExpirationDate.HasValue && l.ExpirationDate.Value < DateTime.UtcNow)
                .ToListAsync();

            // Clean up old inactive links
            var inactiveLinks = await _context.Links
                .Where(l => !l.IsActive && l.CreationDate < cutoffDate)
                .ToListAsync();

            var totalToClean = expiredLinks.Count + inactiveLinks.Count;

            if (totalToClean > 0)
            {
                _context.Links.RemoveRange(expiredLinks);
                _context.Links.RemoveRange(inactiveLinks);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Manual cleanup completed. Removed {TotalCleaned} links", totalToClean);

                return Ok(new
                {
                    Success = true,
                    Message = $"Cleanup completed successfully",
                    ExpiredLinksRemoved = expiredLinks.Count,
                    InactiveLinksRemoved = inactiveLinks.Count,
                    TotalRemoved = totalToClean
                });
            }

            return Ok(new
            {
                Success = true,
                Message = "No links required cleanup",
                ExpiredLinksRemoved = 0,
                InactiveLinksRemoved = 0,
                TotalRemoved = 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual cleanup");
            return StatusCode(500, new { Success = false, Message = "Cleanup failed", Error = ex.Message });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetCleanupStats()
    {
        try
        {
            var retentionDays = _configuration.GetValue<int>("LinkCleanup:RetentionDays", 30);
            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

            var totalLinks = await _context.Links.CountAsync();
            var activeLinks = await _context.Links.CountAsync(l => l.IsActive);
            var expiredLinks = await _context.Links.CountAsync(l => l.ExpirationDate.HasValue && l.ExpirationDate.Value < DateTime.UtcNow);
            var oldInactiveLinks = await _context.Links.CountAsync(l => !l.IsActive && l.CreationDate < cutoffDate);

            return Ok(new
            {
                TotalLinks = totalLinks,
                ActiveLinks = activeLinks,
                ExpiredLinks = expiredLinks,
                OldInactiveLinks = oldInactiveLinks,
                CandidatesForCleanup = expiredLinks + oldInactiveLinks,
                RetentionDays = retentionDays,
                CutoffDate = cutoffDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cleanup stats");
            return StatusCode(500, new { Success = false, Message = "Failed to get stats", Error = ex.Message });
        }
    }
}