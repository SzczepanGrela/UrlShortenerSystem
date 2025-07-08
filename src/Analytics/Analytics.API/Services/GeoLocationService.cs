using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using System.Net;

namespace Analytics.API.Services;

public interface IGeoLocationService
{
    Task<(string? Country, string? City)> GetLocationAsync(string ipAddress);
}

public class GeoLocationService : IGeoLocationService, IDisposable
{
    private readonly DatabaseReader _reader;
    private readonly ILogger<GeoLocationService> _logger;

    public GeoLocationService(IConfiguration configuration, ILogger<GeoLocationService> logger)
    {
        _logger = logger;
        var databasePath = configuration["MaxMind:DatabasePath"];
        
        if (string.IsNullOrEmpty(databasePath))
        {
            throw new InvalidOperationException("MaxMind database path not configured");
        }

        if (!File.Exists(databasePath))
        {
            _logger.LogWarning("MaxMind database file not found at {Path}. Geolocation will be disabled.", databasePath);
            _reader = null!;
            return;
        }

        try
        {
            _reader = new DatabaseReader(databasePath);
            _logger.LogInformation("MaxMind GeoIP2 database loaded successfully from {Path}", databasePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load MaxMind database from {Path}", databasePath);
            _reader = null!;
        }
    }

    public async Task<(string? Country, string? City)> GetLocationAsync(string ipAddress)
    {
        if (_reader == null)
        {
            _logger.LogDebug("GeoIP2 database not available, skipping geolocation for IP: {IpAddress}", ipAddress);
            return (null, null);
        }

        try
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
            {
                _logger.LogWarning("Invalid IP address format: {IpAddress}", ipAddress);
                return (null, null);
            }

            // Skip private/local IP addresses
            if (IsPrivateOrLocalIp(ip))
            {
                _logger.LogDebug("Skipping geolocation for private/local IP: {IpAddress}", ipAddress);
                return (null, null);
            }

            var response = await Task.Run(() => _reader.City(ip));
            
            var country = response.Country?.Name;
            var city = response.City?.Name;

            _logger.LogDebug("Geolocation resolved for IP {IpAddress}: Country={Country}, City={City}", 
                ipAddress, country ?? "Unknown", city ?? "Unknown");

            return (country, city);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve geolocation for IP: {IpAddress}", ipAddress);
            return (null, null);
        }
    }

    private static bool IsPrivateOrLocalIp(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10 ||
                   (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 127);
        }

        return IPAddress.IsLoopback(ip);
    }

    public void Dispose()
    {
        _reader?.Dispose();
    }
}