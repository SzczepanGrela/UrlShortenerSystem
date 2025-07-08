using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using System.Net;
using Xunit;
using UrlShortener.API.Controllers;
using UrlShortener.API.Services;
using UrlShortener.CrossCutting.Dtos;

namespace UrlShortener.Tests
{
    public class RedirectControllerTests
    {
        private readonly Mock<ILinkService> _mockLinkService;
        private readonly Mock<IAnalyticsService> _mockAnalyticsService;
        private readonly Mock<ILogger<RedirectController>> _mockLogger;
        private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
        private readonly Mock<IServiceScope> _mockServiceScope;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly RedirectController _controller;

        public RedirectControllerTests()
        {
            _mockLinkService = new Mock<ILinkService>();
            _mockAnalyticsService = new Mock<IAnalyticsService>();
            _mockLogger = new Mock<ILogger<RedirectController>>();
            _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
            _mockServiceScope = new Mock<IServiceScope>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            
            // Setup service scope factory to return mocked analytics service
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IAnalyticsService)))
                .Returns(_mockAnalyticsService.Object);
            _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
            _mockServiceScopeFactory.Setup(sf => sf.CreateScope()).Returns(_mockServiceScope.Object);
            
            _controller = new RedirectController(_mockLinkService.Object, _mockAnalyticsService.Object, _mockLogger.Object, _mockServiceScopeFactory.Object);
        }

        [Fact]
        public async Task RedirectToOriginal_ValidLink_ShouldReturnRedirect()
        {
            // Arrange
            var shortCode = "ABC123";
            var originalUrl = "https://example.com";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = originalUrl,
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddDays(30)
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(link);

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(originalUrl, redirectResult.Url);
        }

        [Fact]
        public async Task RedirectToOriginal_NonExistentLink_ShouldReturnNotFound()
        {
            // Arrange
            var shortCode = "NONEXISTENT";

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync((LinkDto)null);

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Link not found", notFoundResult.Value);
        }

        [Fact]
        public async Task RedirectToOriginal_ExpiredLink_ShouldReturnGone()
        {
            // Arrange
            var shortCode = "EXPIRED";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = "https://example.com",
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow.AddDays(-60),
                ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired yesterday
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(link);

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(410, statusCodeResult.StatusCode); // HTTP 410 Gone
            Assert.Equal("Link has expired", statusCodeResult.Value);
        }

        [Fact]
        public async Task RedirectToOriginal_ValidLink_ShouldRegisterClickWithAnalytics()
        {
            // Arrange
            var shortCode = "ABC123";
            var originalUrl = "https://example.com";
            var linkId = Guid.NewGuid();
            var link = new LinkDto
            {
                Id = linkId,
                OriginalUrl = originalUrl,
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddDays(30)
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(link);

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(originalUrl, redirectResult.Url);

            // Give some time for the fire-and-forget task to complete
            await Task.Delay(100);

            // Verify analytics was called
            _mockAnalyticsService.Verify(s => s.RegisterClick(
                linkId, 
                originalUrl, 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>()), 
                Times.Once);
        }

        [Fact]
        public async Task RedirectToOriginal_WithHeaders_ShouldPassCorrectDataToAnalytics()
        {
            // Arrange
            var shortCode = "ABC123";
            var originalUrl = "https://example.com";
            var linkId = Guid.NewGuid();
            var link = new LinkDto
            {
                Id = linkId,
                OriginalUrl = originalUrl,
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddDays(30)
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(link);

            SetupHttpContext(
                xForwardedFor: "203.0.113.1",
                userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                referer: "https://google.com/search?q=test"
            );

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(originalUrl, redirectResult.Url);

            // Give some time for the fire-and-forget task to complete
            await Task.Delay(100);

            // Verify analytics was called with correct parameters
            _mockAnalyticsService.Verify(s => s.RegisterClick(
                linkId, 
                originalUrl, 
                "203.0.113.1", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36", 
                "https://google.com/search?q=test"), 
                Times.Once);
        }

        [Fact]
        public async Task RedirectToOriginal_AnalyticsFailure_ShouldStillRedirect()
        {
            // Arrange
            var shortCode = "ABC123";
            var originalUrl = "https://example.com";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = originalUrl,
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddDays(30)
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(link);

            _mockAnalyticsService.Setup(s => s.RegisterClick(
                It.IsAny<Guid>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<string>()))
                .ThrowsAsync(new Exception("Analytics service unavailable"));

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(originalUrl, redirectResult.Url);

            // Give some time for the fire-and-forget task to complete
            await Task.Delay(100);

            // Verify warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Analytics registration failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task RedirectToOriginal_LinkWithoutExpiration_ShouldRedirect()
        {
            // Arrange
            var shortCode = "ABC123";
            var originalUrl = "https://example.com";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = originalUrl,
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow,
                ExpirationDate = null // No expiration
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(link);

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(originalUrl, redirectResult.Url);
        }

        [Fact]
        public async Task RedirectToOriginal_FutureExpirationDate_ShouldRedirect()
        {
            // Arrange
            var shortCode = "ABC123";
            var originalUrl = "https://example.com";
            var link = new LinkDto
            {
                Id = Guid.NewGuid(),
                OriginalUrl = originalUrl,
                ShortCode = shortCode,
                CreationDate = DateTime.UtcNow,
                ExpirationDate = DateTime.UtcNow.AddDays(30) // Expires in the future
            };

            _mockLinkService.Setup(s => s.GetLinkByShortCode(shortCode))
                .ReturnsAsync(link);

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal(originalUrl, redirectResult.Url);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task RedirectToOriginal_EmptyShortCode_ShouldReturnNotFound(string shortCode)
        {
            // Arrange
            _mockLinkService.Setup(s => s.GetLinkByShortCode(It.IsAny<string>()))
                .ReturnsAsync((LinkDto)null);

            SetupHttpContext();

            // Act
            var result = await _controller.RedirectToOriginal(shortCode);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Link not found", notFoundResult.Value);
        }

        private void SetupHttpContext(string xForwardedFor = "192.168.1.1", 
            string userAgent = "TestAgent", 
            string referer = "https://test.com")
        {
            var httpContext = new DefaultHttpContext();
            
            // Setup headers
            httpContext.Request.Headers["X-Forwarded-For"] = new StringValues(xForwardedFor);
            httpContext.Request.Headers["User-Agent"] = new StringValues(userAgent);
            httpContext.Request.Headers["Referer"] = new StringValues(referer);

            // Setup connection info
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }
    }
}