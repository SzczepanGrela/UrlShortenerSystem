using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Analytics.Storage;
using Analytics.Storage.Entities;
using Analytics.CrossCutting.Dtos;
using Analytics.API.Extensions;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace Analytics.API.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AnalyticsDbContext _context;
        private readonly ILogger<AnalyticsService> _logger;
        private readonly IGeoLocationService _geoLocationService;

        public AnalyticsService(AnalyticsDbContext context, ILogger<AnalyticsService> logger, IGeoLocationService geoLocationService)
        {
            _context = context;
            _logger = logger;
            _geoLocationService = geoLocationService;
        }

        public async Task<CrudOperationResult<ClickDto>> RegisterClick(RegisterClickRequestDto request)
        {
            try
            {
                // Pobierz lokalizację na podstawie IP
                var (country, city) = await _geoLocationService.GetLocationAsync(request.IpAddress);

                var click = new Click
                {
                    Id = Guid.NewGuid(),
                    LinkId = request.LinkId,
                    Timestamp = request.Timestamp,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent,
                    Referer = request.Referer,
                    Country = country,
                    City = city
                };

                _context.Clicks.Add(click);
                await _context.SaveChangesAsync();

                return new CrudOperationResult<ClickDto>
                {
                    Result = click.ToDto(),
                    Status = CrudOperationResultStatus.Success
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering click");
                return new CrudOperationResult<ClickDto>
                {
                    Status = CrudOperationResultStatus.Error,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<LinkStatsDto> GetStatsForLink(Guid linkId)
        {
            try
            {
                _logger.LogInformation("Retrieving stats for link: {LinkId}", linkId);

                var clicks = await _context.Clicks
                    .Where(c => c.LinkId == linkId)
                    .OrderBy(c => c.Timestamp)
                    .ToListAsync();

                var stats = new LinkStatsDto
                {
                    LinkId = linkId,
                    TotalClicks = clicks.Count,
                    UniqueClicks = clicks.Select(c => c.IpAddress).Distinct().Count(),
                    FirstClick = clicks.FirstOrDefault()?.Timestamp,
                    LastClick = clicks.LastOrDefault()?.Timestamp
                };

                // Statystyki dzienne
                stats.DailyStats = clicks
                    .GroupBy(c => c.Timestamp.Date)
                    .Select(g => new DailyClickStatsDto
                    {
                        Date = g.Key,
                        Clicks = g.Count()
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                // Statystyki krajów
                stats.CountryStats = clicks
                    .Where(c => !string.IsNullOrEmpty(c.Country))
                    .GroupBy(c => c.Country)
                    .Select(g => new CountryStatsDto
                    {
                        Country = g.Key!,
                        Clicks = g.Count()
                    })
                    .OrderByDescending(c => c.Clicks)
                    .ToList();

                _logger.LogInformation("Successfully retrieved stats for link: {LinkId}, Total clicks: {TotalClicks}", linkId, stats.TotalClicks);
                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stats for link: {LinkId}", linkId);
                throw;
            }
        }

        public async Task<List<ClickDto>> GetClicksForLink(Guid linkId, int page = 1, int pageSize = 50)
        {
            try
            {
                _logger.LogInformation("Retrieving clicks for link: {LinkId}, page: {Page}, pageSize: {PageSize}", linkId, page, pageSize);

                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 50;
                if (pageSize > 1000) pageSize = 1000; // Limit max page size

                var clicks = await _context.Clicks
                    .Where(c => c.LinkId == linkId)
                    .OrderByDescending(c => c.Timestamp)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => c.ToDto())
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {ClickCount} clicks for link: {LinkId}", clicks.Count, linkId);
                return clicks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving clicks for link: {LinkId}", linkId);
                throw;
            }
        }
    }
}