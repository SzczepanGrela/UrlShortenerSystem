namespace UrlShortener.API.Options
{
    public class RateLimitingOptions
    {
        public int MaxRequestsPerMinute { get; set; } = 100;
        public int TimeWindowMinutes { get; set; } = 1;
        public int MaxIpsToTrack { get; set; } = 10000;
        public int CleanupIntervalMinutes { get; set; } = 1;
    }
}