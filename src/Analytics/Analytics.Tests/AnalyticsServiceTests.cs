using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Analytics.API.Services;
using Analytics.Storage;
using Analytics.Storage.Entities;
using Analytics.CrossCutting.Dtos;
using UrlShortenerSystem.Common.CrossCutting.Dtos;
using Xunit;
using Moq;

namespace Analytics.Tests
{
    public class TestAnalyticsDbContext : AnalyticsDbContext
    {
        private readonly DbContextOptions<AnalyticsDbContext> _options;

        public TestAnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options, IConfiguration configuration) 
            : base(configuration)
        {
            _options = options;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
        }
    }

    public class AnalyticsServiceTests
    {
        private TestAnalyticsDbContext GetInMemoryContext()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new TestAnalyticsDbContext(options, configuration);
        }

        [Fact]
        public async Task RegisterClick_ShouldCreateClickSuccessfully()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            var linkId = Guid.NewGuid();
            var request = new RegisterClickRequestDto
            {
                LinkId = linkId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.1",
                UserAgent = "Mozilla/5.0",
                Referer = "https://google.com"
            };

            // Act
            var result = await service.RegisterClick(request);

            // Assert
            Assert.Equal(CrudOperationResultStatus.Success, result.Status);
            Assert.NotNull(result.Result);
            Assert.Equal(linkId, result.Result.LinkId);
            Assert.Equal("192.168.1.1", result.Result.IpAddress);
            Assert.Equal("Mozilla/5.0", result.Result.UserAgent);
            Assert.Equal("https://google.com", result.Result.Referer);
        }

        [Fact]
        public async Task GetStatsForLink_WithNoClicks_ShouldReturnEmptyStats()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            var linkId = Guid.NewGuid();

            // Act
            var stats = await service.GetStatsForLink(linkId);

            // Assert
            Assert.Equal(linkId, stats.LinkId);
            Assert.Equal(0, stats.TotalClicks);
            Assert.Equal(0, stats.UniqueClicks);
            Assert.Null(stats.FirstClick);
            Assert.Null(stats.LastClick);
            Assert.Empty(stats.DailyStats);
            Assert.Empty(stats.CountryStats);
        }

        [Fact]
        public async Task GetStatsForLink_WithClicks_ShouldReturnCorrectStats()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            var linkId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Add test clicks
            var clicks = new[]
            {
                new Click
                {
                    Id = Guid.NewGuid(),
                    LinkId = linkId,
                    Timestamp = now.AddDays(-2),
                    IpAddress = "192.168.1.1",
                    Country = "Poland"
                },
                new Click
                {
                    Id = Guid.NewGuid(),
                    LinkId = linkId,
                    Timestamp = now.AddDays(-1),
                    IpAddress = "192.168.1.2",
                    Country = "Poland"
                },
                new Click
                {
                    Id = Guid.NewGuid(),
                    LinkId = linkId,
                    Timestamp = now,
                    IpAddress = "192.168.1.1", // Same IP as first click
                    Country = "Germany"
                }
            };

            context.Clicks.AddRange(clicks);
            await context.SaveChangesAsync();

            // Act
            var stats = await service.GetStatsForLink(linkId);

            // Assert
            Assert.Equal(linkId, stats.LinkId);
            Assert.Equal(3, stats.TotalClicks);
            Assert.Equal(2, stats.UniqueClicks); // Two unique IPs
            Assert.Equal(now.AddDays(-2).Date, stats.FirstClick?.Date);
            Assert.Equal(now.Date, stats.LastClick?.Date);
            
            // Check daily stats
            Assert.Equal(3, stats.DailyStats.Count);
            Assert.Contains(stats.DailyStats, d => d.Date.Date == now.AddDays(-2).Date && d.Clicks == 1);
            Assert.Contains(stats.DailyStats, d => d.Date.Date == now.AddDays(-1).Date && d.Clicks == 1);
            Assert.Contains(stats.DailyStats, d => d.Date.Date == now.Date && d.Clicks == 1);

            // Check country stats
            Assert.Equal(2, stats.CountryStats.Count);
            Assert.Contains(stats.CountryStats, c => c.Country == "Poland" && c.Clicks == 2);
            Assert.Contains(stats.CountryStats, c => c.Country == "Germany" && c.Clicks == 1);
        }

        [Fact]
        public async Task GetClicksForLink_ShouldReturnClicksWithPagination()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            var linkId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Add test clicks
            var clicks = new List<Click>();
            for (int i = 0; i < 10; i++)
            {
                clicks.Add(new Click
                {
                    Id = Guid.NewGuid(),
                    LinkId = linkId,
                    Timestamp = now.AddMinutes(-i),
                    IpAddress = $"192.168.1.{i}",
                    UserAgent = $"Agent-{i}"
                });
            }

            context.Clicks.AddRange(clicks);
            await context.SaveChangesAsync();

            // Act - Get first page (5 items)
            var page1 = await service.GetClicksForLink(linkId, page: 1, pageSize: 5);
            
            // Act - Get second page (5 items)
            var page2 = await service.GetClicksForLink(linkId, page: 2, pageSize: 5);

            // Assert
            Assert.Equal(5, page1.Count);
            Assert.Equal(5, page2.Count);
            
            // Should be ordered by timestamp descending (newest first)
            Assert.True(page1[0].Timestamp > page1[1].Timestamp);
            Assert.True(page1[1].Timestamp > page1[2].Timestamp);
            
            // No overlap between pages
            var page1Ids = page1.Select(c => c.Id).ToHashSet();
            var page2Ids = page2.Select(c => c.Id).ToHashSet();
            Assert.Empty(page1Ids.Intersect(page2Ids));
        }

        [Fact]
        public async Task GetClicksForLink_WithDifferentLinks_ShouldReturnOnlyRelevantClicks()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            var linkId1 = Guid.NewGuid();
            var linkId2 = Guid.NewGuid();

            // Add clicks for different links
            var clicks = new[]
            {
                new Click { Id = Guid.NewGuid(), LinkId = linkId1, Timestamp = DateTime.UtcNow, IpAddress = "192.168.1.1" },
                new Click { Id = Guid.NewGuid(), LinkId = linkId1, Timestamp = DateTime.UtcNow, IpAddress = "192.168.1.2" },
                new Click { Id = Guid.NewGuid(), LinkId = linkId2, Timestamp = DateTime.UtcNow, IpAddress = "192.168.1.3" }
            };

            context.Clicks.AddRange(clicks);
            await context.SaveChangesAsync();

            // Act
            var link1Clicks = await service.GetClicksForLink(linkId1);
            var link2Clicks = await service.GetClicksForLink(linkId2);

            // Assert
            Assert.Equal(2, link1Clicks.Count);
            Assert.Single(link2Clicks);
            Assert.All(link1Clicks, c => Assert.Equal(linkId1, c.LinkId));
            Assert.All(link2Clicks, c => Assert.Equal(linkId2, c.LinkId));
        }

        [Fact]
        public async Task RegisterClick_WithError_ShouldReturnErrorResult()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new TestAnalyticsDbContext(options, configuration);
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            // Dispose context to simulate database error
            context.Dispose();

            var request = new RegisterClickRequestDto
            {
                LinkId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.1"
            };

            // Act
            var result = await service.RegisterClick(request);

            // Assert
            Assert.Equal(CrudOperationResultStatus.Error, result.Status);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Result);
        }

        [Fact]
        public async Task GetStatsForLink_ShouldOrderDailyStatsByDate()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            var linkId = Guid.NewGuid();
            var baseDate = DateTime.UtcNow.Date;

            // Add clicks in random order
            var clicks = new[]
            {
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = baseDate.AddDays(2) },
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = baseDate },
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = baseDate.AddDays(1) }
            };

            context.Clicks.AddRange(clicks);
            await context.SaveChangesAsync();

            // Act
            var stats = await service.GetStatsForLink(linkId);

            // Assert
            Assert.Equal(3, stats.DailyStats.Count);
            Assert.Equal(baseDate, stats.DailyStats[0].Date);
            Assert.Equal(baseDate.AddDays(1), stats.DailyStats[1].Date);
            Assert.Equal(baseDate.AddDays(2), stats.DailyStats[2].Date);
        }

        [Fact]
        public async Task GetStatsForLink_ShouldOrderCountryStatsByClicksDescending()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var logger = new Mock<ILogger<AnalyticsService>>();
            var geoLocationService = new Mock<IGeoLocationService>();
            var service = new AnalyticsService(context, logger.Object, geoLocationService.Object);

            var linkId = Guid.NewGuid();

            // Add clicks with different countries
            var clicks = new[]
            {
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = DateTime.UtcNow, Country = "Poland" },
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = DateTime.UtcNow, Country = "Germany" },
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = DateTime.UtcNow, Country = "Germany" },
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = DateTime.UtcNow, Country = "Germany" },
                new Click { Id = Guid.NewGuid(), LinkId = linkId, Timestamp = DateTime.UtcNow, Country = "Poland" }
            };

            context.Clicks.AddRange(clicks);
            await context.SaveChangesAsync();

            // Act
            var stats = await service.GetStatsForLink(linkId);

            // Assert
            Assert.Equal(2, stats.CountryStats.Count);
            Assert.Equal("Germany", stats.CountryStats[0].Country);
            Assert.Equal(3, stats.CountryStats[0].Clicks);
            Assert.Equal("Poland", stats.CountryStats[1].Country);
            Assert.Equal(2, stats.CountryStats[1].Clicks);
        }
    }
}