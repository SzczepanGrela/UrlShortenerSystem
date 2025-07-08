using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Analytics.API.Services;
using Analytics.CrossCutting.Dtos;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace Analytics.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Rejestruje nowe kliknięcie
        /// </summary>
        /// <param name="request">Dane kliknięcia</param>
        /// <returns>Zarejestrowane kliknięcie</returns>
        [HttpPost("clicks")]
        public async Task<IActionResult> RegisterClick([FromBody] RegisterClickRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _analyticsService.RegisterClick(request);

            if (result.Status == CrudOperationResultStatus.Success)
            {
                return Ok(result.Result);
            }

            return BadRequest(result.ErrorMessage);
        }

        /// <summary>
        /// Pobiera statystyki dla linku
        /// </summary>
        /// <param name="linkId">ID linku</param>
        /// <returns>Statystyki</returns>
        [HttpGet("links/{linkId}/stats")]
        public async Task<IActionResult> GetStatsForLink(Guid linkId)
        {
            if (linkId == Guid.Empty)
            {
                return BadRequest("Valid link ID is required");
            }

            var stats = await _analyticsService.GetStatsForLink(linkId);
            return Ok(stats);
        }

        /// <summary>
        /// Pobiera kliknięcia dla linku
        /// </summary>
        /// <param name="linkId">ID linku</param>
        /// <param name="page">Strona</param>
        /// <param name="pageSize">Rozmiar strony</param>
        /// <returns>Lista kliknięć</returns>
        [HttpGet("links/{linkId}/clicks")]
        public async Task<IActionResult> GetClicksForLink(Guid linkId, [FromQuery][Range(1, int.MaxValue)] int page = 1, [FromQuery][Range(1, 1000)] int pageSize = 50)
        {
            if (linkId == Guid.Empty)
            {
                return BadRequest("Valid link ID is required");
            }

            if (page < 1)
            {
                return BadRequest("Page number must be greater than 0");
            }

            if (pageSize < 1 || pageSize > 1000)
            {
                return BadRequest("Page size must be between 1 and 1000");
            }

            var clicks = await _analyticsService.GetClicksForLink(linkId, page, pageSize);
            return Ok(clicks);
        }
    }
}