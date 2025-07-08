using Analytics.CrossCutting.Dtos;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace Analytics.API.Services
{
    public interface IAnalyticsService
    {
        Task<CrudOperationResult<ClickDto>> RegisterClick(RegisterClickRequestDto request);
        Task<LinkStatsDto> GetStatsForLink(Guid linkId);
        Task<List<ClickDto>> GetClicksForLink(Guid linkId, int page = 1, int pageSize = 50);
    }
}