using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.CrossCutting.Dtos;
using UrlShortener.API.Options;

namespace UrlShortener.API.Services
{
    public interface ICacheService
    {
        Task<LinkDto?> GetLinkAsync(string shortCode);
        Task SetLinkAsync(string shortCode, LinkDto link);
        Task RemoveLinkAsync(string shortCode);
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
    }

    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<CacheService> _logger;
        private readonly CacheOptions _options;

        public CacheService(IMemoryCache memoryCache, ILogger<CacheService> logger, IOptions<CacheOptions> options)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<LinkDto?> GetLinkAsync(string shortCode)
        {
            try
            {
                var cacheKey = $"link:{shortCode}";
                if (_memoryCache.TryGetValue(cacheKey, out LinkDto? cachedLink))
                {
                    _logger.LogDebug("Cache hit for link: {ShortCode}", shortCode);
                    return cachedLink;
                }

                _logger.LogDebug("Cache miss for link: {ShortCode}", shortCode);
                await Task.CompletedTask;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving link from cache: {ShortCode}", shortCode);
                await Task.CompletedTask;
                return null;
            }
        }

        public async Task SetLinkAsync(string shortCode, LinkDto link)
        {
            try
            {
                var cacheKey = $"link:{shortCode}";
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
                    Priority = Enum.Parse<CacheItemPriority>(_options.Priority, true)
                };

                _memoryCache.Set(cacheKey, link, cacheEntryOptions);
                _logger.LogDebug("Cached link: {ShortCode}", shortCode);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching link: {ShortCode}", shortCode);
                await Task.CompletedTask;
            }
        }

        public async Task RemoveLinkAsync(string shortCode)
        {
            try
            {
                var cacheKey = $"link:{shortCode}";
                _memoryCache.Remove(cacheKey);
                _logger.LogDebug("Removed link from cache: {ShortCode}", shortCode);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing link from cache: {ShortCode}", shortCode);
                await Task.CompletedTask;
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out T? cachedValue))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    return cachedValue;
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                await Task.CompletedTask;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving value from cache: {Key}", key);
                await Task.CompletedTask;
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
                    Priority = Enum.Parse<CacheItemPriority>(_options.Priority, true)
                };

                _memoryCache.Set(key, value, cacheEntryOptions);
                _logger.LogDebug("Cached value for key: {Key}", key);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching value for key: {Key}", key);
                await Task.CompletedTask;
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _memoryCache.Remove(key);
                _logger.LogDebug("Removed value from cache: {Key}", key);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing value from cache: {Key}", key);
                await Task.CompletedTask;
            }
        }
    }
}