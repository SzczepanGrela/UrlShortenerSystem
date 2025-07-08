namespace UrlShortener.API.Services
{
    public interface IUrlNormalizationService
    {
        string NormalizeUrl(string url);
    }
}