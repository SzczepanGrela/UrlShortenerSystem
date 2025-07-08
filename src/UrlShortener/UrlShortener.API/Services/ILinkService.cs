using UrlShortener.CrossCutting.Dtos;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace UrlShortener.API.Services
{
    public interface ILinkService
    {
        Task<CrudOperationResult<LinkDto>> CreateLink(CreateLinkRequestDto request);
        Task<LinkDto?> GetLinkByShortCode(string shortCode);
        Task<CrudOperationResult<bool>> DeleteLink(Guid id);
    }
}