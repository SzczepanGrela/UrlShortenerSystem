using Analytics.Storage.Entities;
using Analytics.CrossCutting.Dtos;

namespace Analytics.API.Extensions
{
    public static class ClickExtensions
    {
        public static ClickDto ToDto(this Click entity)
        {
            return new ClickDto
            {
                Id = entity.Id,
                LinkId = entity.LinkId,
                Timestamp = entity.Timestamp,
                IpAddress = entity.IpAddress,
                UserAgent = entity.UserAgent,
                Referer = entity.Referer,
                Country = entity.Country,
                City = entity.City
            };
        }
    }
}