using Microsoft.Extensions.Configuration;
using UrlShortener.Storage.Entities;
using UrlShortener.CrossCutting.Dtos;

namespace UrlShortener.API.Extensions
{
    public static class LinkExtensions
    {
        public static LinkDto ToDto(this Link entity, IConfiguration configuration)
        {
            var baseUrl = configuration["BaseUrl"] ?? "https://localhost:7000";
            
            return new LinkDto
            {
                Id = entity.Id,
                OriginalUrl = entity.OriginalUrl,
                ShortCode = entity.ShortCode,
                ShortUrl = $"{baseUrl}/{entity.ShortCode}",
                CreationDate = entity.CreationDate,
                ExpirationDate = entity.ExpirationDate,
                IsActive = entity.IsActive
            };
        }
    }
}