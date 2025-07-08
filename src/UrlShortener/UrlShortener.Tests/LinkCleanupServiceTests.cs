using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using UrlShortener.API.Services;
using UrlShortener.Storage;
using UrlShortener.Storage.Entities;

namespace UrlShortener.Tests;

public class LinkCleanupServiceTests : IDisposable
{
    private readonly UrlShortenerDbContext _context;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LinkCleanupService> _logger;

    public LinkCleanupServiceTests()
    {
        var services = new ServiceCollection();
        
        // Add configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            {"LinkCleanup:RetentionDays", "30"},
            {"LinkCleanup:IntervalHours", "6"}
        });
        _configuration = configBuilder.Build();
        services.AddSingleton(_configuration);

        // Add logging
        services.AddLogging();

        // Use TestUrlShortenerDbContext for in-memory testing
        var options = new DbContextOptionsBuilder<UrlShortenerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TestUrlShortenerDbContext(options, _configuration);
        
        // Register the context in DI for LinkCleanupService
        services.AddSingleton<UrlShortenerDbContext>(_context);
        
        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<LinkCleanupService>>();

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task PerformCleanup_ShouldRemoveExpiredLinks()
    {
        // Arrange
        var expiredLink = new Link
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/expired",
            ShortCode = "exp123",
            CreationDate = DateTime.UtcNow.AddDays(-5),
            ExpirationDate = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            IsActive = true
        };

        var activeLink = new Link
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/active",
            ShortCode = "act123",
            CreationDate = DateTime.UtcNow.AddDays(-1),
            ExpirationDate = DateTime.UtcNow.AddDays(1), // Expires tomorrow
            IsActive = true
        };

        _context.Links.AddRange(expiredLink, activeLink);
        await _context.SaveChangesAsync();

        var cleanupService = new LinkCleanupService(_serviceProvider, _logger, _configuration);

        // Act
        using var cancellationTokenSource = new CancellationTokenSource();
        await cleanupService.PerformCleanupAsync(30, cancellationTokenSource.Token);

        // Assert
        var remainingLinks = await _context.Links.ToListAsync();
        Assert.Single(remainingLinks);
        Assert.Equal("act123", remainingLinks[0].ShortCode);
    }

    [Fact]
    public async Task PerformCleanup_ShouldRemoveOldInactiveLinks()
    {
        // Arrange
        var oldInactiveLink = new Link
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/old-inactive",
            ShortCode = "old123",
            CreationDate = DateTime.UtcNow.AddDays(-45), // Created 45 days ago
            ExpirationDate = null,
            IsActive = false // Inactive
        };

        var recentInactiveLink = new Link
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/recent-inactive",
            ShortCode = "rec123",
            CreationDate = DateTime.UtcNow.AddDays(-5), // Created 5 days ago
            ExpirationDate = null,
            IsActive = false // Inactive but recent
        };

        var activeLink = new Link
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/active",
            ShortCode = "act123",
            CreationDate = DateTime.UtcNow.AddDays(-50),
            ExpirationDate = null,
            IsActive = true // Active
        };

        _context.Links.AddRange(oldInactiveLink, recentInactiveLink, activeLink);
        await _context.SaveChangesAsync();

        var cleanupService = new LinkCleanupService(_serviceProvider, _logger, _configuration);

        // Act
        using var cancellationTokenSource = new CancellationTokenSource();
        await cleanupService.PerformCleanupAsync(30, cancellationTokenSource.Token);

        // Assert
        var remainingLinks = await _context.Links.ToListAsync();
        Assert.Equal(2, remainingLinks.Count);
        Assert.Contains(remainingLinks, l => l.ShortCode == "rec123");
        Assert.Contains(remainingLinks, l => l.ShortCode == "act123");
        Assert.DoesNotContain(remainingLinks, l => l.ShortCode == "old123");
    }

    [Fact]
    public async Task PerformCleanup_NoLinksToClean_ShouldNotRemoveAnything()
    {
        // Arrange
        var activeLink = new Link
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com/active",
            ShortCode = "act123",
            CreationDate = DateTime.UtcNow.AddDays(-1),
            ExpirationDate = DateTime.UtcNow.AddDays(1),
            IsActive = true
        };

        _context.Links.Add(activeLink);
        await _context.SaveChangesAsync();

        var cleanupService = new LinkCleanupService(_serviceProvider, _logger, _configuration);

        // Act
        using var cancellationTokenSource = new CancellationTokenSource();
        await cleanupService.PerformCleanupAsync(30, cancellationTokenSource.Token);

        // Assert
        var remainingLinks = await _context.Links.ToListAsync();
        Assert.Single(remainingLinks);
        Assert.Equal("act123", remainingLinks[0].ShortCode);
    }

    [Fact]
    public async Task PerformCleanup_MixedScenario_ShouldCleanupCorrectly()
    {
        // Arrange
        var links = new[]
        {
            new Link // Should be removed - expired
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com/expired1",
                ShortCode = "exp1",
                CreationDate = DateTime.UtcNow.AddDays(-10),
                ExpirationDate = DateTime.UtcNow.AddDays(-2),
                IsActive = true
            },
            new Link // Should be removed - old inactive
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com/old-inactive",
                ShortCode = "old1",
                CreationDate = DateTime.UtcNow.AddDays(-50),
                ExpirationDate = null,
                IsActive = false
            },
            new Link // Should remain - active and not expired
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com/active1",
                ShortCode = "act1",
                CreationDate = DateTime.UtcNow.AddDays(-5),
                ExpirationDate = DateTime.UtcNow.AddDays(5),
                IsActive = true
            },
            new Link // Should remain - recent inactive
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com/recent-inactive",
                ShortCode = "rec1",
                CreationDate = DateTime.UtcNow.AddDays(-5),
                ExpirationDate = null,
                IsActive = false
            },
            new Link // Should remain - no expiration date and active
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com/permanent",
                ShortCode = "perm1",
                CreationDate = DateTime.UtcNow.AddDays(-100),
                ExpirationDate = null,
                IsActive = true
            }
        };

        _context.Links.AddRange(links);
        await _context.SaveChangesAsync();

        var cleanupService = new LinkCleanupService(_serviceProvider, _logger, _configuration);

        // Act
        using var cancellationTokenSource = new CancellationTokenSource();
        await cleanupService.PerformCleanupAsync(30, cancellationTokenSource.Token);

        // Assert
        var remainingLinks = await _context.Links.ToListAsync();
        Assert.Equal(3, remainingLinks.Count);
        
        var remainingCodes = remainingLinks.Select(l => l.ShortCode).ToList();
        Assert.Contains("act1", remainingCodes);
        Assert.Contains("rec1", remainingCodes);
        Assert.Contains("perm1", remainingCodes);
        
        Assert.DoesNotContain("exp1", remainingCodes);
        Assert.DoesNotContain("old1", remainingCodes);
    }

    public void Dispose()
    {
        _context?.Dispose();
        if (_serviceProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
    }

    public class TestUrlShortenerDbContext : UrlShortenerDbContext
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