using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.API.Options;

namespace UrlShortener.API.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;
        private readonly RateLimitingOptions _options;
        private static readonly ConcurrentDictionary<string, List<DateTime>> _requestHistory = new();
        private static readonly Timer _cleanupTimer;
        private static readonly object _timerLock = new object();
        private static volatile bool _disposed = false;

        static RateLimitingMiddleware()
        {
            _cleanupTimer = new Timer(
                _ => {
                    if (!_disposed)
                        Cleanup();
                }, 
                null, 
                TimeSpan.FromMinutes(1), 
                TimeSpan.FromMinutes(1));
        }

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IOptions<RateLimitingOptions> options)
        {
            _next = next;
            _logger = logger;
            _options = options.Value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            
            // Skip rate limiting for invalid or unknown IPs
            if (string.IsNullOrEmpty(clientIp) || clientIp == "unknown")
            {
                await _next(context);
                return;
            }
            
            if (IsRateLimited(clientIp))
            {
                _logger.LogWarning("Rate limit exceeded for IP: {ClientIp}", clientIp);
                
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                
                var response = new
                {
                    error = "Rate limit exceeded",
                    message = $"Maximum {_options.MaxRequestsPerMinute} requests per minute allowed",
                    retryAfter = _options.TimeWindowMinutes * 60
                };
                
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
                return;
            }

            await _next(context);
        }

        private string GetClientIpAddress(HttpContext context)
        {
            // Try to get the real IP address from headers (for proxy scenarios)
            var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xForwardedFor))
            {
                var ips = xForwardedFor.Split(',');
                if (ips.Length > 0)
                {
                    return ips[0].Trim();
                }
            }

            var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(xRealIp))
            {
                return xRealIp;
            }

            // Fallback to connection remote IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private bool IsRateLimited(string clientIp)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.Subtract(TimeSpan.FromMinutes(_options.TimeWindowMinutes));

            // Check if we're tracking too many IPs
            if (_requestHistory.Count >= _options.MaxIpsToTrack)
            {
                _logger.LogWarning("Rate limiting dictionary is full. Cleaning up old entries.");
                Cleanup();
            }

            // Get or create request history for this IP
            var requests = _requestHistory.GetOrAdd(clientIp, _ => new List<DateTime>());

            lock (requests)
            {
                // Remove old requests outside the time window
                requests.RemoveAll(r => r < cutoff);

                // Check if we're at the rate limit
                if (requests.Count >= _options.MaxRequestsPerMinute)
                {
                    return true;
                }

                // Add current request
                requests.Add(now);
                return false;
            }
        }

        // Cleanup method to prevent memory leaks
        public static void Cleanup()
        {
            var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5));
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _requestHistory)
            {
                lock (kvp.Value)
                {
                    kvp.Value.RemoveAll(r => r < cutoff);
                    
                    if (kvp.Value.Count == 0)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _requestHistory.TryRemove(key, out _);
            }
        }

        // Dispose timer when application shuts down
        public static void Dispose()
        {
            lock (_timerLock)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _cleanupTimer?.Dispose();
                }
            }
        }
    }

    public static class RateLimitingMiddlewareExtensions
    {
        public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitingMiddleware>();
        }
    }
}