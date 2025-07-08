namespace UrlShortener.API.Options
{
    public class LinkGenerationOptions
    {
        public int DefaultCodeLength { get; set; } = 6;
        public int MaxRetries { get; set; } = 5;
        public int BatchSize { get; set; } = 10;
        public string AllowedCharacters { get; set; } = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        public int SmallScaleThreshold { get; set; } = 100000;
        public int MediumScaleThreshold { get; set; } = 1000000;
        public int LargeScaleThreshold { get; set; } = 10000000;
        public int SmallScaleCodeLength { get; set; } = 6;
        public int MediumScaleCodeLength { get; set; } = 7;
        public int LargeScaleCodeLength { get; set; } = 8;
        public int ExtraLargeScaleCodeLength { get; set; } = 9;
    }
}