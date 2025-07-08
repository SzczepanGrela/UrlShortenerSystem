using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UrlShortener.Storage;

namespace UrlShortener.Tests;

public class TestWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup>
    where TStartup : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing UrlShortenerDbContext registration
            var contextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(UrlShortenerDbContext));
            if (contextDescriptor != null)
            {
                services.Remove(contextDescriptor);
            }

            // Configure test-specific settings first
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"ConnectionStrings:UrlShortener", "InMemory"}, // Override the SQL Server connection
                    {"LinkCleanup:RetentionDays", "30"},
                    {"LinkCleanup:IntervalHours", "6"},
                    {"Caching:Enabled", "false"},
                    {"RateLimiting:MaxRequestsPerMinute", "2"}, // Very low limit for testing
                    {"RateLimiting:TimeWindowMinutes", "1"},
                    {"RateLimiting:MaxIpsToTrack", "1000"}
                });
            });

            // Re-configure RateLimitingOptions after the configuration is set
            services.Configure<UrlShortener.API.Options.RateLimitingOptions>(options =>
            {
                options.MaxRequestsPerMinute = 2;
                options.TimeWindowMinutes = 1;
                options.MaxIpsToTrack = 1000;
            });

            // Register a custom UrlShortenerDbContext that uses in-memory database
            services.AddScoped<UrlShortenerDbContext>(serviceProvider =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var options = new DbContextOptionsBuilder<UrlShortenerDbContext>()
                    .UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString())
                    .EnableSensitiveDataLogging()
                    .Options;
                
                return new IntegrationTestDbContext(options, configuration);
            });
        });

        builder.UseEnvironment("Testing");
    }
}

public class IntegrationTestDbContext : UrlShortenerDbContext
{
    private readonly DbContextOptions<UrlShortenerDbContext> _options;

    public IntegrationTestDbContext(DbContextOptions<UrlShortenerDbContext> options, IConfiguration configuration) 
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