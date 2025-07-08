using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using UrlShortener.API.Services;
using UrlShortener.API.Options;
using UrlShortener.CrossCutting.Dtos;

namespace UrlShortener.Tests
{
    public class CacheServiceTests : IDisposable
    {
        private readonly Mock<ILogger<CacheService>> _mockLogger;
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<IOptions<CacheOptions>> _mockOptions;
        private readonly CacheService _service;

        public CacheServiceTests()
        {
            _mockLogger = new Mock<ILogger<CacheService>>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _mockOptions = new Mock<IOptions<CacheOptions>>();
            _mockOptions.Setup(x => x.Value).Returns(new CacheOptions());
            _service = new CacheService(_memoryCache, _mockLogger.Object, _mockOptions.Object);
        }

        [Fact]
        public async Task GetLinkAsync_ExistingLink_ShouldReturnCachedLink()
        {
            // Arrange
            var shortCode = "ABC123";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow
            };

            await _service.SetLinkAsync(shortCode, link);

            // Act
            var result = await _service.GetLinkAsync(shortCode);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(link.Id, result.Id);
            Assert.Equal(link.OriginalUrl, result.OriginalUrl);
            Assert.Equal(link.ShortCode, result.ShortCode);
        }

        [Fact]
        public async Task GetLinkAsync_NonExistingLink_ShouldReturnNull()
        {
            // Arrange
            var shortCode = "NONEXISTENT";

            // Act
            var result = await _service.GetLinkAsync(shortCode);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetLinkAsync_ValidLink_ShouldCacheSuccessfully()
        {
            // Arrange
            var shortCode = "ABC123";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow
            };

            // Act
            await _service.SetLinkAsync(shortCode, link);

            // Assert
            var cachedLink = await _service.GetLinkAsync(shortCode);
            Assert.NotNull(cachedLink);
            Assert.Equal(link.Id, cachedLink.Id);
            Assert.Equal(link.OriginalUrl, cachedLink.OriginalUrl);
        }

        [Fact]
        public async Task RemoveLinkAsync_ExistingLink_ShouldRemoveFromCache()
        {
            // Arrange
            var shortCode = "ABC123";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow
            };

            await _service.SetLinkAsync(shortCode, link);

            // Act
            await _service.RemoveLinkAsync(shortCode);

            // Assert
            var cachedLink = await _service.GetLinkAsync(shortCode);
            Assert.Null(cachedLink);
        }

        [Fact]
        public async Task GetAsync_ExistingGenericValue_ShouldReturnCachedValue()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";

            await _service.SetAsync(key, value);

            // Act
            var result = await _service.GetAsync<string>(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task GetAsync_NonExistingGenericValue_ShouldReturnNull()
        {
            // Arrange
            var key = "nonexistent-key";

            // Act
            var result = await _service.GetAsync<string>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetAsync_WithCustomExpiration_ShouldCacheWithSpecifiedExpiration()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";
            var expiration = TimeSpan.FromSeconds(1);

            // Act
            await _service.SetAsync(key, value, expiration);

            // Assert
            var cachedValue = await _service.GetAsync<string>(key);
            Assert.NotNull(cachedValue);
            Assert.Equal(value, cachedValue);
        }

        [Fact]
        public async Task SetAsync_WithoutCustomExpiration_ShouldUseDefaultExpiration()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";

            // Act
            await _service.SetAsync(key, value);

            // Assert
            var cachedValue = await _service.GetAsync<string>(key);
            Assert.NotNull(cachedValue);
            Assert.Equal(value, cachedValue);
        }

        [Fact]
        public async Task RemoveAsync_ExistingGenericValue_ShouldRemoveFromCache()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";

            await _service.SetAsync(key, value);

            // Act
            await _service.RemoveAsync(key);

            // Assert
            var cachedValue = await _service.GetAsync<string>(key);
            Assert.Null(cachedValue);
        }

        [Fact]
        public async Task GetLinkAsync_CacheHit_ShouldLogDebugMessage()
        {
            // Arrange
            var shortCode = "ABC123";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow
            };

            await _service.SetLinkAsync(shortCode, link);

            // Act
            await _service.GetLinkAsync(shortCode);

            // Assert
            VerifyDebugLogged("Cache hit for link:");
        }

        [Fact]
        public async Task GetLinkAsync_CacheMiss_ShouldLogDebugMessage()
        {
            // Arrange
            var shortCode = "NONEXISTENT";

            // Act
            await _service.GetLinkAsync(shortCode);

            // Assert
            VerifyDebugLogged("Cache miss for link:");
        }

        [Fact]
        public async Task SetLinkAsync_ValidLink_ShouldLogDebugMessage()
        {
            // Arrange
            var shortCode = "ABC123";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow
            };

            // Act
            await _service.SetLinkAsync(shortCode, link);

            // Assert
            VerifyDebugLogged("Cached link:");
        }

        [Fact]
        public async Task RemoveLinkAsync_ExistingLink_ShouldLogDebugMessage()
        {
            // Arrange
            var shortCode = "ABC123";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow
            };

            await _service.SetLinkAsync(shortCode, link);

            // Act
            await _service.RemoveLinkAsync(shortCode);

            // Assert
            VerifyDebugLogged("Removed link from cache:");
        }

        [Fact]
        public async Task GetAsync_CacheHit_ShouldLogDebugMessage()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";

            await _service.SetAsync(key, value);

            // Act
            await _service.GetAsync<string>(key);

            // Assert
            VerifyDebugLogged("Cache hit for key:");
        }

        [Fact]
        public async Task GetAsync_CacheMiss_ShouldLogDebugMessage()
        {
            // Arrange
            var key = "nonexistent-key";

            // Act
            await _service.GetAsync<string>(key);

            // Assert
            VerifyDebugLogged("Cache miss for key:");
        }

        [Fact]
        public async Task SetAsync_ValidValue_ShouldLogDebugMessage()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";

            // Act
            await _service.SetAsync(key, value);

            // Assert
            VerifyDebugLogged("Cached value for key:");
        }

        [Fact]
        public async Task RemoveAsync_ExistingValue_ShouldLogDebugMessage()
        {
            // Arrange
            var key = "test-key";
            var value = "test-value";

            await _service.SetAsync(key, value);

            // Act
            await _service.RemoveAsync(key);

            // Assert
            VerifyDebugLogged("Removed value from cache:");
        }

        [Fact]
        public async Task GetLinkAsync_NullShortCode_ShouldHandleGracefully()
        {
            // Act & Assert
            var result = await _service.GetLinkAsync(null);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetLinkAsync_EmptyShortCode_ShouldHandleGracefully()
        {
            // Act & Assert
            var result = await _service.GetLinkAsync("");
            Assert.Null(result);
        }

        [Fact]
        public async Task SetLinkAsync_NullShortCode_ShouldHandleGracefully()
        {
            // Arrange
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = "ABC123",
                CreationDate = DateTime.UtcNow
            };

            // Act & Assert - Should not throw
            await _service.SetLinkAsync(null, link);
        }

        [Fact]
        public async Task SetLinkAsync_NullLink_ShouldHandleGracefully()
        {
            // Act & Assert - Should not throw
            await _service.SetLinkAsync("ABC123", null);
        }

        [Fact]
        public async Task GetAsync_DifferentTypes_ShouldWorkIndependently()
        {
            // Arrange
            var stringKey = "string-key";
            var stringValue = "string-value";
            var intKey = "int-key";
            var intValue = new List<int> { 1, 2, 3 };

            await _service.SetAsync(stringKey, stringValue);
            await _service.SetAsync(intKey, intValue);

            // Act
            var stringResult = await _service.GetAsync<string>(stringKey);
            var intResult = await _service.GetAsync<List<int>>(intKey);

            // Assert
            Assert.Equal(stringValue, stringResult);
            Assert.Equal(intValue, intResult);
        }

        private void VerifyDebugLogged(string messageContains)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }
    }
}