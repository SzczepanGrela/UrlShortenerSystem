using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UrlShortener.API.Services
{
    public interface IUrlValidationService
    {
        Task<bool> IsValidUrlAsync(string url);
        bool IsValidUrlFormat(string url);
    }

    public class UrlValidationService : IUrlValidationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UrlValidationService> _logger;

        public UrlValidationService(HttpClient httpClient, ILogger<UrlValidationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public bool IsValidUrlFormat(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // Check for extremely long URLs (security measure)
            if (url.Length > 2048)
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            // Only allow HTTP and HTTPS schemes
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return false;

            // Check for malicious schemes that might bypass Uri.TryCreate
            var lowerUrl = url.ToLowerInvariant();
            if (lowerUrl.StartsWith("javascript:") || 
                lowerUrl.StartsWith("data:") || 
                lowerUrl.StartsWith("vbscript:") || 
                lowerUrl.StartsWith("file:") ||
                lowerUrl.StartsWith("ftp:"))
                return false;

            // Check for local network addresses (SSRF protection)
            if (IsLocalNetworkAddress(uri.Host))
                return false;

            // Check for SQL injection patterns in URL
            if (ContainsSqlInjectionPatterns(url))
                return false;

            return true;
        }

        private bool IsLocalNetworkAddress(string host)
        {
            if (string.IsNullOrEmpty(host))
                return false;

            var lowerHost = host.ToLowerInvariant();
            
            // Check for localhost variations
            if (lowerHost == "localhost" || lowerHost == "127.0.0.1" || lowerHost == "0.0.0.0" || lowerHost == "[::1]")
                return true;

            // Parse IP address if possible
            if (System.Net.IPAddress.TryParse(host, out var ipAddress))
            {
                // Check for IPv4 private ranges
                var bytes = ipAddress.GetAddressBytes();
                if (bytes.Length == 4) // IPv4
                {
                    // 10.0.0.0/8
                    if (bytes[0] == 10)
                        return true;
                    
                    // 172.16.0.0/12
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                        return true;
                    
                    // 192.168.0.0/16
                    if (bytes[0] == 192 && bytes[1] == 168)
                        return true;
                    
                    // 127.0.0.0/8 (loopback)
                    if (bytes[0] == 127)
                        return true;
                    
                    // 169.254.0.0/16 (link-local)
                    if (bytes[0] == 169 && bytes[1] == 254)
                        return true;
                }
                else if (bytes.Length == 16) // IPv6
                {
                    // Check for IPv6 loopback and link-local
                    if (ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal || 
                        ipAddress.Equals(System.Net.IPAddress.IPv6Loopback))
                        return true;
                }
            }

            return false;
        }

        private bool ContainsSqlInjectionPatterns(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;

            var lowerUrl = url.ToLowerInvariant();
            
            // Common SQL injection patterns
            string[] sqlPatterns = {
                "' or ",
                "\" or ",
                "' and ",
                "\" and ",
                "drop table",
                "delete from",
                "insert into",
                "update set",
                "union select",
                "exec(",
                "execute(",
                "sp_",
                "xp_",
                "--",
                ";--",
                "/*",
                "*/"
            };

            foreach (var pattern in sqlPatterns)
            {
                if (lowerUrl.Contains(pattern))
                    return true;
            }

            return false;
        }

        public async Task<bool> IsValidUrlAsync(string url)
        {
            if (!IsValidUrlFormat(url))
                return false;

            try
            {
                _httpClient.Timeout = TimeSpan.FromSeconds(10);
                var response = await _httpClient.GetAsync(url);
                
                // Accept any response that's not a client error (4xx) or server error (5xx)
                return (int)response.StatusCode < 400;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("URL validation failed for {Url}: {Error}", url, ex.Message);
                return false;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("URL validation timeout for {Url}", url);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during URL validation for {Url}", url);
                return false;
            }
        }
    }
}