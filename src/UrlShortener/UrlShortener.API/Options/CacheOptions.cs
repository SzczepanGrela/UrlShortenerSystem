using System;

namespace UrlShortener.API.Options
{
    public class CacheOptions
    {
        public int AbsoluteExpirationMinutes { get; set; } = 30;
        public int SlidingExpirationMinutes { get; set; } = 5;
        public string Priority { get; set; } = "Normal";
    }
}