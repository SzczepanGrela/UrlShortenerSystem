using Microsoft.Extensions.Options;
using UrlShortener.API.Services;
using UrlShortener.API.Options;
using UrlShortener.Storage;

namespace UrlShortener.API.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddUrlShortenerServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure options
            services.Configure<CacheOptions>(configuration.GetSection("Cache"));
            services.Configure<LinkGenerationOptions>(configuration.GetSection("LinkGeneration"));
            services.Configure<RateLimitingOptions>(configuration.GetSection("RateLimiting"));
            
            // Configure HttpClient options
            var httpClientOptions = configuration.GetSection("HttpClient").Get<HttpClientOptions>() ?? new HttpClientOptions();
            
            // Core services
            services.AddTransient<ILinkService, LinkService>();
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddDbContext<UrlShortenerDbContext>();
            services.AddHttpClient<AnalyticsService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(httpClientOptions.AnalyticsTimeoutSeconds);
            });
            
            // New Phase VI services
            services.AddScoped<IUrlValidationService, UrlValidationService>();
            services.AddScoped<ICacheService, CacheService>();
            services.AddScoped<IUrlNormalizationService, UrlNormalizationService>();
            services.AddHttpClient<UrlValidationService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(httpClientOptions.UrlValidationTimeoutSeconds);
            });
            
            // Memory cache
            services.AddMemoryCache();
            
            // Logging
            services.AddLogging();
            
            return services;
        }
    }
}