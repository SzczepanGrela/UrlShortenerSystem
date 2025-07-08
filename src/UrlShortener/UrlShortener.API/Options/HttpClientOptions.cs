namespace UrlShortener.API.Options
{
    public class HttpClientOptions
    {
        public int AnalyticsTimeoutSeconds { get; set; } = 30;
        public int UrlValidationTimeoutSeconds { get; set; } = 10;
        public int DefaultTimeoutSeconds { get; set; } = 30;
    }
}