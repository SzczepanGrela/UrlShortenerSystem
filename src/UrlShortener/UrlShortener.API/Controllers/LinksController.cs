using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using UrlShortener.API.Services;
using UrlShortener.CrossCutting.Dtos;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace UrlShortener.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LinksController : ControllerBase
    {
        private readonly ILinkService _linkService;

        public LinksController(ILinkService linkService)
        {
            _linkService = linkService;
        }

        /// <summary>
        /// Tworzy nowy skrócony link
        /// </summary>
        /// <param name="request">Dane do utworzenia linku</param>
        /// <returns>Utworzony link</returns>
        [HttpPost]
        public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _linkService.CreateLink(request);

            if (result.Status == CrudOperationResultStatus.Success)
            {
                return CreatedAtAction(nameof(GetLinkByShortCode), 
                    new { shortCode = result.Result!.ShortCode }, result.Result);
            }

            return BadRequest(result.ErrorMessage);
        }

        /// <summary>
        /// Pobiera link po short code
        /// </summary>
        /// <param name="shortCode">Krótki kod</param>
        /// <returns>Link</returns>
        [HttpGet("{shortCode}")]
        public async Task<IActionResult> GetLinkByShortCode([Required][StringLength(20, MinimumLength = 1)] string shortCode)
        {
            if (string.IsNullOrWhiteSpace(shortCode))
            {
                return BadRequest("Short code is required");
            }

            var link = await _linkService.GetLinkByShortCode(shortCode);

            if (link == null)
            {
                return NotFound();
            }

            return Ok(link);
        }

        /// <summary>
        /// Usuwa link
        /// </summary>
        /// <param name="id">ID linku</param>
        /// <returns>Wynik operacji</returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteLink(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest("Valid link ID is required");
            }

            var result = await _linkService.DeleteLink(id);

            return result.Status switch
            {
                CrudOperationResultStatus.Success => Ok(),
                CrudOperationResultStatus.NotFound => NotFound(),
                _ => BadRequest(result.ErrorMessage)
            };
        }
    }
}