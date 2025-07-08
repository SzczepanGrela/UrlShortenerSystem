using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrlShortener.Storage;
using UrlShortener.Storage.Entities;
using UrlShortener.CrossCutting.Dtos;
using UrlShortener.API.Extensions;
using UrlShortener.API.Options;
using UrlShortenerSystem.Common.CrossCutting.Dtos;

namespace UrlShortener.API.Services
{
    public class LinkService : ILinkService
    {
        private readonly UrlShortenerDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IUrlValidationService _urlValidationService;
        private readonly ICacheService _cacheService;
        private readonly IUrlNormalizationService _urlNormalizationService;
        private readonly ILogger<LinkService> _logger;
        private readonly LinkGenerationOptions _linkGenerationOptions;

        public LinkService(UrlShortenerDbContext context, IConfiguration configuration, IUrlValidationService urlValidationService, ICacheService cacheService, IUrlNormalizationService urlNormalizationService, ILogger<LinkService> logger, IOptions<LinkGenerationOptions> linkGenerationOptions)
        {
            _context = context;
            _configuration = configuration;
            _urlValidationService = urlValidationService;
            _cacheService = cacheService;
            _urlNormalizationService = urlNormalizationService;
            _logger = logger;
            _linkGenerationOptions = linkGenerationOptions.Value;
        }

        public async Task<CrudOperationResult<LinkDto>> CreateLink(CreateLinkRequestDto request)
        {
            _logger.LogInformation("Creating link for URL: {OriginalUrl}", request.OriginalUrl);
            
            try
            {
                // Validate URL format first
                if (!_urlValidationService.IsValidUrlFormat(request.OriginalUrl))
                {
                    _logger.LogWarning("Invalid URL format provided: {OriginalUrl}", request.OriginalUrl);
                    return new CrudOperationResult<LinkDto>
                    {
                        Status = CrudOperationResultStatus.Error,
                        ErrorMessage = "Invalid URL format"
                    };
                }

                // Check for deduplication
                var deduplicationEnabled = _configuration.GetValue<bool>("LinkDeduplication:Enabled", true);
                var refreshExpirationDate = _configuration.GetValue<bool>("LinkDeduplication:RefreshExpirationDate", true);
                var normalizeUrls = _configuration.GetValue<bool>("LinkDeduplication:NormalizeUrls", true);
                
                var urlToCheck = normalizeUrls ? _urlNormalizationService.NormalizeUrl(request.OriginalUrl) : request.OriginalUrl;
                
                if (deduplicationEnabled)
                {
                    var existingLink = await _context.Links
                        .FirstOrDefaultAsync(l => l.OriginalUrl == urlToCheck && l.IsActive);
                    
                    if (existingLink != null)
                    {
                        _logger.LogInformation("Found existing link for URL: {OriginalUrl}, ShortCode: {ShortCode}", 
                            urlToCheck, existingLink.ShortCode);
                        
                        // Update expiration date if requested
                        if (refreshExpirationDate && request.ExpirationDate != existingLink.ExpirationDate)
                        {
                            _logger.LogInformation("Updating expiration date for existing link: {ShortCode}", 
                                existingLink.ShortCode);
                            existingLink.ExpirationDate = request.ExpirationDate;
                            await _context.SaveChangesAsync();
                        }
                        
                        var existingLinkDto = existingLink.ToDto(_configuration);
                        
                        // Update cache with potentially new expiration date
                        await _cacheService.SetLinkAsync(existingLink.ShortCode, existingLinkDto);
                        
                        return new CrudOperationResult<LinkDto>
                        {
                            Result = existingLinkDto,
                            Status = CrudOperationResultStatus.Success
                        };
                    }
                }

                // Optional: Check if URL is reachable (can be disabled for performance)
                // if (!await _urlValidationService.IsValidUrlAsync(request.OriginalUrl))
                // {
                //     return new CrudOperationResult<LinkDto>
                //     {
                //         Status = CrudOperationResultStatus.Error,
                //         ErrorMessage = "URL is not reachable"
                //     };
                // }

                var shortCode = await GenerateUniqueShortCodeAsync();

                var link = new Link
                {
                    Id = Guid.NewGuid(),
                    OriginalUrl = urlToCheck,
                    ShortCode = shortCode,
                    CreationDate = DateTime.UtcNow,
                    ExpirationDate = request.ExpirationDate,
                    IsActive = true
                };

                _context.Links.Add(link);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully created new link: {ShortCode} for URL: {OriginalUrl}", 
                    shortCode, urlToCheck);

                var linkDto = link.ToDto(_configuration);
                
                // Cache the newly created link
                await _cacheService.SetLinkAsync(shortCode, linkDto);

                _logger.LogInformation("Link creation completed successfully: {ShortCode}", shortCode);
                
                return new CrudOperationResult<LinkDto>
                {
                    Result = linkDto,
                    Status = CrudOperationResultStatus.Success
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating link for URL: {OriginalUrl}", request.OriginalUrl);
                return new CrudOperationResult<LinkDto>
                {
                    Status = CrudOperationResultStatus.Error,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<LinkDto?> GetLinkByShortCode(string shortCode)
        {
            _logger.LogInformation("Retrieving link for short code: {ShortCode}", shortCode);
            
            // Try to get from cache first
            var cachedLink = await _cacheService.GetLinkAsync(shortCode);
            if (cachedLink != null)
            {
                _logger.LogInformation("Link found in cache: {ShortCode}", shortCode);
                return cachedLink;
            }

            // If not in cache, get from database
            var link = await _context.Links
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ShortCode == shortCode && l.IsActive);

            if (link != null)
            {
                _logger.LogInformation("Link found in database: {ShortCode}, caching for future requests", shortCode);
                var linkDto = link.ToDto(_configuration);
                // Cache the link for future requests
                await _cacheService.SetLinkAsync(shortCode, linkDto);
                return linkDto;
            }

            _logger.LogWarning("Link not found: {ShortCode}", shortCode);
            return null;
        }

        public async Task<CrudOperationResult<bool>> DeleteLink(Guid id)
        {
            _logger.LogInformation("Deleting link with ID: {LinkId}", id);
            
            try
            {
                var link = await _context.Links.FindAsync(id);
                if (link == null)
                {
                    _logger.LogWarning("Link not found for deletion: {LinkId}", id);
                    return new CrudOperationResult<bool>
                    {
                        Status = CrudOperationResultStatus.NotFound
                    };
                }

                _logger.LogInformation("Soft deleting link: {ShortCode}", link.ShortCode);
                
                link.IsActive = false;
                await _context.SaveChangesAsync();

                // Remove from cache when deleted
                await _cacheService.RemoveLinkAsync(link.ShortCode);
                
                _logger.LogInformation("Link deleted successfully: {ShortCode}", link.ShortCode);

                return new CrudOperationResult<bool>
                {
                    Result = true,
                    Status = CrudOperationResultStatus.Success
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting link with ID: {LinkId}", id);
                return new CrudOperationResult<bool>
                {
                    Status = CrudOperationResultStatus.Error,
                    ErrorMessage = ex.Message
                };
            }
        }

        private async Task<string> GenerateUniqueShortCodeAsync()
        {
            // Calculate code length based on existing links count for better collision avoidance
            var existingLinksCount = await _context.Links.CountAsync();
            var codeLength = CalculateOptimalCodeLength(existingLinksCount);
            
            for (int retry = 0; retry < _linkGenerationOptions.MaxRetries; retry++)
            {
                // Generate batch of potential codes
                var candidateCodes = GenerateShortCodeBatch(_linkGenerationOptions.BatchSize, codeLength);
                
                // Check which codes already exist in database
                var existingCodes = await _context.Links
                    .Where(l => candidateCodes.Contains(l.ShortCode))
                    .Select(l => l.ShortCode)
                    .ToListAsync();
                
                // Return first available code
                var availableCode = candidateCodes.FirstOrDefault(code => !existingCodes.Contains(code));
                if (availableCode != null)
                {
                    return availableCode;
                }
            }
            
            // Fallback: generate longer code if all retries failed
            return GenerateShortCode(codeLength + 2);
        }
        
        private List<string> GenerateShortCodeBatch(int batchSize, int codeLength)
        {
            var codes = new List<string>(batchSize);
            
            for (int i = 0; i < batchSize; i++)
            {
                codes.Add(GenerateShortCode(codeLength));
            }
            
            return codes;
        }
        
        private string GenerateShortCode(int length = 6)
        {
            var chars = _linkGenerationOptions.AllowedCharacters;
            var shortCode = new char[length];
            
            for (int i = 0; i < shortCode.Length; i++)
            {
                shortCode[i] = chars[Random.Shared.Next(chars.Length)];
            }
            
            return new string(shortCode);
        }
        
        private int CalculateOptimalCodeLength(int existingLinksCount)
        {
            if (existingLinksCount < _linkGenerationOptions.SmallScaleThreshold) 
                return _linkGenerationOptions.SmallScaleCodeLength;
            if (existingLinksCount < _linkGenerationOptions.MediumScaleThreshold) 
                return _linkGenerationOptions.MediumScaleCodeLength;
            if (existingLinksCount < _linkGenerationOptions.LargeScaleThreshold) 
                return _linkGenerationOptions.LargeScaleCodeLength;
            return _linkGenerationOptions.ExtraLargeScaleCodeLength;
        }
    }
}