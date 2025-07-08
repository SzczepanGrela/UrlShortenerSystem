using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using UrlShortener.API.Services;
using UrlShortener.API.Options;
using UrlShortener.Storage;
using UrlShortener.Storage.Entities;
using UrlShortener.CrossCutting.Dtos;
using Xunit;
using Moq;

namespace UrlShortener.Tests
{
    public class TestUrlShortenerDbContext : UrlShortenerDbContext
    {
        private readonly DbContextOptions<UrlShortenerDbContext> _options;

        public TestUrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options, IConfiguration configuration) 
            : base(configuration)
        {
            _options = options;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
        }
    }

    public class LinkServiceTests
    {
        private TestUrlShortenerDbContext GetInMemoryContext()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"BaseUrl", "https://localhost:7000"}
                })
                .Build();

            var options = new DbContextOptionsBuilder<UrlShortenerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            return new TestUrlShortenerDbContext(options, configuration);
        }

        private IConfiguration GetTestConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    {"BaseUrl", "https://localhost:7000"}
                })
                .Build();
        }

        private Mock<IUrlValidationService> GetMockUrlValidationService()
        {
            var mock = new Mock<IUrlValidationService>();
            mock.Setup(x => x.IsValidUrlFormat(It.IsAny<string>())).Returns(true);
            mock.Setup(x => x.IsValidUrlAsync(It.IsAny<string>())).ReturnsAsync(true);
            return mock;
        }

        private Mock<ICacheService> GetMockCacheService()
        {
            var mock = new Mock<ICacheService>();
            mock.Setup(x => x.GetAsync<LinkDto>(It.IsAny<string>())).ReturnsAsync((LinkDto)null);
            return mock;
        }

        private Mock<IUrlNormalizationService> GetMockUrlNormalizationService()
        {
            var mock = new Mock<IUrlNormalizationService>();
            mock.Setup(x => x.NormalizeUrl(It.IsAny<string>())).Returns((string url) => url);
            return mock;
        }

        [Fact]
        public async Task CreateLink_ShouldCreateValidLink()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var configuration = GetTestConfiguration();
            
            var urlValidationService = GetMockUrlValidationService();
            var cacheService = GetMockCacheService();
            var urlNormalizationService = GetMockUrlNormalizationService();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LinkService>>();
            var linkGenerationOptions = new Mock<IOptions<LinkGenerationOptions>>();
            linkGenerationOptions.Setup(x => x.Value).Returns(new LinkGenerationOptions());
            var service = new LinkService(context, configuration, urlValidationService.Object, cacheService.Object, urlNormalizationService.Object, logger.Object, linkGenerationOptions.Object);
            var request = new CreateLinkRequestDto
            {
                OriginalUrl = "https://www.example.com"
            };

            // Act
            var result = await service.CreateLink(request);

            // Assert
            Assert.NotNull(result.Result);
            Assert.Equal("https://www.example.com", result.Result.OriginalUrl);
            Assert.NotNull(result.Result.ShortCode);
            Assert.Equal(6, result.Result.ShortCode.Length);
            Assert.Equal("https://localhost:7000/" + result.Result.ShortCode, result.Result.ShortUrl);
        }

        [Fact]
        public async Task GetLinkByShortCode_ShouldReturnCorrectLink()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var configuration = GetTestConfiguration();
            
            var urlValidationService = GetMockUrlValidationService();
            var cacheService = GetMockCacheService();
            var urlNormalizationService = GetMockUrlNormalizationService();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LinkService>>();
            var linkGenerationOptions = new Mock<IOptions<LinkGenerationOptions>>();
            linkGenerationOptions.Setup(x => x.Value).Returns(new LinkGenerationOptions());
            var service = new LinkService(context, configuration, urlValidationService.Object, cacheService.Object, urlNormalizationService.Object, logger.Object, linkGenerationOptions.Object);
            var request = new CreateLinkRequestDto
            {
                OriginalUrl = "https://www.example.com"
            };

            // Act
            var createResult = await service.CreateLink(request);
            var getResult = await service.GetLinkByShortCode(createResult.Result!.ShortCode);

            // Assert
            Assert.NotNull(getResult);
            Assert.Equal("https://www.example.com", getResult.OriginalUrl);
            Assert.Equal(createResult.Result.ShortCode, getResult.ShortCode);
        }

        [Fact]
        public async Task GetLinkByShortCode_ShouldReturnNullForNonExistentCode()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var configuration = GetTestConfiguration();
            
            var urlValidationService = GetMockUrlValidationService();
            var cacheService = GetMockCacheService();
            var urlNormalizationService = GetMockUrlNormalizationService();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LinkService>>();
            var linkGenerationOptions = new Mock<IOptions<LinkGenerationOptions>>();
            linkGenerationOptions.Setup(x => x.Value).Returns(new LinkGenerationOptions());
            var service = new LinkService(context, configuration, urlValidationService.Object, cacheService.Object, urlNormalizationService.Object, logger.Object, linkGenerationOptions.Object);

            // Act
            var result = await service.GetLinkByShortCode("NONEXISTENT");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteLink_ShouldMarkAsInactive()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var configuration = GetTestConfiguration();
            
            var urlValidationService = GetMockUrlValidationService();
            var cacheService = GetMockCacheService();
            var urlNormalizationService = GetMockUrlNormalizationService();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LinkService>>();
            var linkGenerationOptions = new Mock<IOptions<LinkGenerationOptions>>();
            linkGenerationOptions.Setup(x => x.Value).Returns(new LinkGenerationOptions());
            var service = new LinkService(context, configuration, urlValidationService.Object, cacheService.Object, urlNormalizationService.Object, logger.Object, linkGenerationOptions.Object);
            var request = new CreateLinkRequestDto { OriginalUrl = "https://www.example.com" };
            var createResult = await service.CreateLink(request);

            // Act
            var deleteResult = await service.DeleteLink(createResult.Result!.Id);

            // Assert
            Assert.NotNull(deleteResult.Result);
            Assert.True(deleteResult.Result);
            
            // Verify it's not returned by GetLinkByShortCode (because IsActive = false)
            var getResult = await service.GetLinkByShortCode(createResult.Result.ShortCode);
            Assert.Null(getResult);
        }

        [Fact]
        public async Task CreateLink_WithExpirationDate_ShouldSetExpiration()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var configuration = GetTestConfiguration();
            
            var urlValidationService = GetMockUrlValidationService();
            var cacheService = GetMockCacheService();
            var urlNormalizationService = GetMockUrlNormalizationService();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LinkService>>();
            var linkGenerationOptions = new Mock<IOptions<LinkGenerationOptions>>();
            linkGenerationOptions.Setup(x => x.Value).Returns(new LinkGenerationOptions());
            var service = new LinkService(context, configuration, urlValidationService.Object, cacheService.Object, urlNormalizationService.Object, logger.Object, linkGenerationOptions.Object);
            var expirationDate = DateTime.UtcNow.AddDays(30);
            var request = new CreateLinkRequestDto
            {
                OriginalUrl = "https://www.example.com",
                ExpirationDate = expirationDate
            };

            // Act
            var result = await service.CreateLink(request);

            // Assert
            Assert.NotNull(result.Result);
            Assert.Equal(expirationDate.Date, result.Result.ExpirationDate?.Date);
        }

        [Fact]
        public async Task CreateLink_ShouldGenerateUniqueShortCodes()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var configuration = GetTestConfiguration();
            
            var urlValidationService = GetMockUrlValidationService();
            var cacheService = GetMockCacheService();
            var urlNormalizationService = GetMockUrlNormalizationService();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LinkService>>();
            var linkGenerationOptions = new Mock<IOptions<LinkGenerationOptions>>();
            linkGenerationOptions.Setup(x => x.Value).Returns(new LinkGenerationOptions());
            var service = new LinkService(context, configuration, urlValidationService.Object, cacheService.Object, urlNormalizationService.Object, logger.Object, linkGenerationOptions.Object);

            // Act - Create multiple links
            var results = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var request = new CreateLinkRequestDto
                {
                    OriginalUrl = $"https://www.example{i}.com"
                };
                var result = await service.CreateLink(request);
                results.Add(result.Result!.ShortCode);
            }

            // Assert - All short codes should be unique
            Assert.Equal(10, results.Distinct().Count());
        }

        [Fact]
        public async Task GetLinkByShortCode_WithInactiveLink_ShouldReturnNull()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var configuration = GetTestConfiguration();
            
            // Directly add an inactive link to the database
            var link = new Link
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://www.example.com",
                ShortCode = "INACTIVE",
                CreationDate = DateTime.UtcNow,
                IsActive = false
            };
            context.Links.Add(link);
            await context.SaveChangesAsync();

            var urlValidationService = GetMockUrlValidationService();
            var cacheService = GetMockCacheService();
            var urlNormalizationService = GetMockUrlNormalizationService();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LinkService>>();
            var linkGenerationOptions = new Mock<IOptions<LinkGenerationOptions>>();
            linkGenerationOptions.Setup(x => x.Value).Returns(new LinkGenerationOptions());
            var service = new LinkService(context, configuration, urlValidationService.Object, cacheService.Object, urlNormalizationService.Object, logger.Object, linkGenerationOptions.Object);

            // Act
            var result = await service.GetLinkByShortCode("INACTIVE");

            // Assert
            Assert.Null(result);
        }
    }
}