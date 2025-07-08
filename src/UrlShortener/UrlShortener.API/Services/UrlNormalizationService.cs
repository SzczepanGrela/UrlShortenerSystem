using Microsoft.Extensions.Logging;

namespace UrlShortener.API.Services;

public class UrlNormalizationService : IUrlNormalizationService
{
    private readonly ILogger<UrlNormalizationService> _logger;

    public UrlNormalizationService(ILogger<UrlNormalizationService> logger)
    {
        _logger = logger;
    }

    public string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Attempted to normalize null or empty URL");
            return url;
        }

        try
        {
            var uri = new Uri(url);
            
            // Normalize scheme to lowercase
            var normalizedScheme = uri.Scheme.ToLowerInvariant();
            
            // Normalize host to lowercase
            var normalizedHost = uri.Host.ToLowerInvariant();
            
            // Remove default ports
            var port = uri.Port;
            if ((normalizedScheme == "http" && port == 80) || 
                (normalizedScheme == "https" && port == 443))
            {
                port = -1;
            }
            
            // Remove trailing slash from path if it's just "/"
            var path = uri.AbsolutePath;
            if (path == "/")
            {
                path = "";
            }
            
            // Reconstruct normalized URL
            var normalizedUrl = $"{normalizedScheme}://{normalizedHost}";
            
            if (port != -1)
            {
                normalizedUrl += $":{port}";
            }
            
            normalizedUrl += path;
            
            if (!string.IsNullOrEmpty(uri.Query))
            {
                normalizedUrl += uri.Query;
            }
            
            if (!string.IsNullOrEmpty(uri.Fragment))
            {
                normalizedUrl += uri.Fragment;
            }
            
            _logger.LogDebug("Successfully normalized URL from {OriginalUrl} to {NormalizedUrl}", url, normalizedUrl);
            return normalizedUrl;
        }
        catch (UriFormatException ex)
        {
            _logger.LogWarning(ex, "Invalid URL format during normalization: {Url}", url);
            return url;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid argument during URL normalization: {Url}", url);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during URL normalization: {Url}", url);
            return url;
        }
    }
}