using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UrlShortener.API.Controllers;
using UrlShortener.Storage;
using UrlShortener.Storage.Entities;

namespace UrlShortener.Tests
{
    public class MaintenanceControllerTests : IDisposable
    {
        private readonly Mock<ILogger<MaintenanceController>> _mockLogger;
        private readonly IConfiguration _configuration;
        private readonly UrlShortenerDbContext _context;
        private readonly MaintenanceController _controller;

        public MaintenanceControllerTests()
        {
            _mockLogger = new Mock<ILogger<MaintenanceController>>();
            
            var options = new DbContextOptionsBuilder<UrlShortenerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"LinkCleanup:RetentionDays", "30"},
                {"LinkCleanup:IntervalHours", "6"}
            });
            _configuration = configBuilder.Build();

            _context = new TestUrlShortenerDbContext(options, _configuration);
            _controller = new MaintenanceController(_context, _mockLogger.Object, _configuration);
        }

        [Fact]
        public async Task ManualCleanup_WithExpiredLinks_ShouldRemoveExpiredLinksOnly()
        {
            // Arrange
            var expiredLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://expired.com",
                ShortCode = "EXPIRED",
                CreationDate = DateTime.UtcNow.AddDays(-10),
                ExpirationDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
                IsActive = true
            };

            var activeLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://active.com",
                ShortCode = "ACTIVE",
                CreationDate = DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddDays(30), // Expires in future
                IsActive = true
            };

            _context.Links.AddRange(expiredLink, activeLink);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.ManualCleanup();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            
            Assert.NotNull(response);
            int expiredRemoved = response.ExpiredLinksRemoved;
            int inactiveRemoved = response.InactiveLinksRemoved;
            int totalRemoved = response.TotalRemoved;

            Assert.True(response.Success);
            Assert.Equal(1, response.ExpiredLinksRemoved);
            Assert.Equal(0, response.InactiveLinksRemoved);
            Assert.Equal(1, response.TotalRemoved);

            // Verify database state
            var remainingLinks = await _context.Links.ToListAsync();
            Assert.Single(remainingLinks);
            Assert.Equal("ACTIVE", remainingLinks[0].ShortCode);
        }

        [Fact]
        public async Task ManualCleanup_WithOldInactiveLinks_ShouldRemoveOldInactiveLinksOnly()
        {
            // Arrange
            var oldInactiveLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://oldinactive.com",
                ShortCode = "OLDINACTIVE",
                CreationDate = DateTime.UtcNow.AddDays(-60), // Older than retention period
                IsActive = false
            };

            var recentInactiveLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://recentinactive.com",
                ShortCode = "RECENTINACTIVE",
                CreationDate = DateTime.UtcNow.AddDays(-10), // Within retention period
                IsActive = false
            };

            _context.Links.AddRange(oldInactiveLink, recentInactiveLink);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.ManualCleanup();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            
            Assert.NotNull(response);
            int expiredRemoved = response.ExpiredLinksRemoved;
            int inactiveRemoved = response.InactiveLinksRemoved;
            int totalRemoved = response.TotalRemoved;

            Assert.True(response.Success);
            Assert.Equal(0, response.ExpiredLinksRemoved);
            Assert.Equal(1, response.InactiveLinksRemoved);
            Assert.Equal(1, response.TotalRemoved);

            // Verify database state
            var remainingLinks = await _context.Links.ToListAsync();
            Assert.Single(remainingLinks);
            Assert.Equal("RECENTINACTIVE", remainingLinks[0].ShortCode);
        }

        [Fact]
        public async Task ManualCleanup_WithNoLinksToClean_ShouldReturnSuccessWithZeroCounts()
        {
            // Arrange
            var activeLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://active.com",
                ShortCode = "ACTIVE",
                CreationDate = DateTime.UtcNow,
                IsActive = true
            };

            _context.Links.Add(activeLink);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.ManualCleanup();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            
            Assert.NotNull(response);
            bool success = response.Success;
            string message = response.Message;
            int expiredRemoved = response.ExpiredLinksRemoved;
            int inactiveRemoved = response.InactiveLinksRemoved;
            int totalRemoved = response.TotalRemoved;

            Assert.True(response.Success);
            Assert.Equal("No links required cleanup", response.Message);
            Assert.Equal(0, response.ExpiredLinksRemoved);
            Assert.Equal(0, response.InactiveLinksRemoved);
            Assert.Equal(0, response.TotalRemoved);

            // Verify no links were removed
            var remainingLinks = await _context.Links.ToListAsync();
            Assert.Single(remainingLinks);
        }

        [Fact]
        public async Task ManualCleanup_WithCustomRetentionDays_ShouldUseCustomValue()
        {
            // Arrange - Create a separate controller with custom configuration
            var customConfigBuilder = new ConfigurationBuilder();
            customConfigBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"LinkCleanup:RetentionDays", "7"},
                {"LinkCleanup:IntervalHours", "6"}
            });
            var customConfiguration = customConfigBuilder.Build();
            var customController = new MaintenanceController(_context, _mockLogger.Object, customConfiguration);

            var linkWithinCustomRetention = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://recent.com",
                ShortCode = "RECENT",
                CreationDate = DateTime.UtcNow.AddDays(-5), // Within 7 days
                IsActive = false
            };

            var linkBeyondCustomRetention = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://old.com",
                ShortCode = "OLD",
                CreationDate = DateTime.UtcNow.AddDays(-10), // Beyond 7 days
                IsActive = false
            };

            _context.Links.AddRange(linkWithinCustomRetention, linkBeyondCustomRetention);
            await _context.SaveChangesAsync();

            // Act
            var result = await customController.ManualCleanup();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            
            Assert.NotNull(response);
            int inactiveRemoved = response.InactiveLinksRemoved;

            Assert.Equal(1, inactiveRemoved);

            // Verify only the old link was removed
            var remainingLinks = await _context.Links.ToListAsync();
            Assert.Single(remainingLinks);
            Assert.Equal("RECENT", remainingLinks[0].ShortCode);
        }

        [Fact]
        public async Task GetCleanupStats_ShouldReturnCorrectStats()
        {
            // Arrange
            var activeLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://active.com",
                ShortCode = "ACTIVE",
                CreationDate = DateTime.UtcNow,
                IsActive = true
            };

            var expiredLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://expired.com",
                ShortCode = "EXPIRED",
                CreationDate = DateTime.UtcNow.AddDays(-10),
                ExpirationDate = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            var oldInactiveLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://oldinactive.com",
                ShortCode = "OLDINACTIVE",
                CreationDate = DateTime.UtcNow.AddDays(-60),
                IsActive = false
            };

            var recentInactiveLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://recentinactive.com",
                ShortCode = "RECENTINACTIVE",
                CreationDate = DateTime.UtcNow.AddDays(-10),
                IsActive = false
            };

            _context.Links.AddRange(activeLink, expiredLink, oldInactiveLink, recentInactiveLink);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetCleanupStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            
            Assert.NotNull(response);
            int totalLinks = response.TotalLinks;
            int activeLinks = response.ActiveLinks;
            int expiredLinks = response.ExpiredLinks;
            int oldInactiveLinks = response.OldInactiveLinks;
            int candidatesForCleanup = response.CandidatesForCleanup;
            int retentionDays = response.RetentionDays;

            Assert.Equal(4, totalLinks);
            Assert.Equal(2, activeLinks); // activeLink and expiredLink (still marked as active)
            Assert.Equal(1, expiredLinks);
            Assert.Equal(1, oldInactiveLinks);
            Assert.Equal(2, candidatesForCleanup); // expired + old inactive
            Assert.Equal(30, retentionDays);
        }

        [Fact]
        public async Task GetCleanupStats_WithNoLinks_ShouldReturnZeroStats()
        {
            // Act
            var result = await _controller.GetCleanupStats();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            
            Assert.NotNull(response);
            int totalLinks = response.TotalLinks;
            int candidatesForCleanup = response.CandidatesForCleanup;

            Assert.Equal(0, totalLinks);
            Assert.Equal(0, candidatesForCleanup);
        }

        [Fact]
        public async Task ManualCleanup_WithMixedLinksToClean_ShouldCleanBothExpiredAndOldInactive()
        {
            // Arrange
            var expiredLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://expired.com",
                ShortCode = "EXPIRED",
                CreationDate = DateTime.UtcNow.AddDays(-10),
                ExpirationDate = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            var oldInactiveLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://oldinactive.com",
                ShortCode = "OLDINACTIVE",
                CreationDate = DateTime.UtcNow.AddDays(-60),
                IsActive = false
            };

            var activeLink = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://active.com",
                ShortCode = "ACTIVE",
                CreationDate = DateTime.UtcNow,
                IsActive = true
            };

            _context.Links.AddRange(expiredLink, oldInactiveLink, activeLink);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.ManualCleanup();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            dynamic response = okResult.Value;
            
            Assert.NotNull(response);
            int expiredRemoved = response.ExpiredLinksRemoved;
            int inactiveRemoved = response.InactiveLinksRemoved;
            int totalRemoved = response.TotalRemoved;

            Assert.Equal(1, expiredRemoved);
            Assert.Equal(1, inactiveRemoved);
            Assert.Equal(2, totalRemoved);

            // Verify only active link remains
            var remainingLinks = await _context.Links.ToListAsync();
            Assert.Single(remainingLinks);
            Assert.Equal("ACTIVE", remainingLinks[0].ShortCode);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        private class TestUrlShortenerDbContext : UrlShortenerDbContext
        {
            private readonly DbContextOptions<UrlShortenerDbContext> _options;

            public TestUrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options, IConfiguration configuration) 
                : base(configuration)
            {
                _options = options;
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                if (!optionsBuilder.IsConfigured)
                {
                    optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());
                }
            }
        }
    }
}