using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using Xunit;
using UrlShortener.API.Services;

namespace UrlShortener.Tests
{
    public class UrlValidationServiceTests
    {
        private readonly Mock<ILogger<UrlValidationService>> _mockLogger;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly UrlValidationService _service;

        public UrlValidationServiceTests()
        {
            _mockLogger = new Mock<ILogger<UrlValidationService>>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
            _service = new UrlValidationService(_httpClient, _mockLogger.Object);
        }

        [Theory]
        [InlineData("https://example.com")]
        [InlineData("http://example.com")]
        [InlineData("https://subdomain.example.com")]
        [InlineData("http://example.com/path")]
        [InlineData("https://example.com/path?query=value")]
        [InlineData("https://example.com:8080")]
        public void IsValidUrlFormat_ValidUrls_ShouldReturnTrue(string url)
        {
            // Act
            var result = _service.IsValidUrlFormat(url);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com")]
        [InlineData("javascript:alert('xss')")]
        [InlineData("file:///etc/passwd")]
        [InlineData("data:text/html,<script>alert('xss')</script>")]
        [InlineData("example.com")] // Missing scheme
        [InlineData("://example.com")] // Missing scheme
        [InlineData("http://")] // Missing host
        [InlineData("https://")] // Missing host
        public void IsValidUrlFormat_InvalidUrls_ShouldReturnFalse(string url)
        {
            // Act
            var result = _service.IsValidUrlFormat(url);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsValidUrlAsync_ValidUrlWithSuccessResponse_ShouldReturnTrue()
        {
            // Arrange
            var url = "https://example.com";
            SetupHttpResponse(HttpStatusCode.OK);

            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsValidUrlAsync_ValidUrlWithRedirectResponse_ShouldReturnTrue()
        {
            // Arrange
            var url = "https://example.com";
            SetupHttpResponse(HttpStatusCode.Found); // 302 redirect

            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)] // 400
        [InlineData(HttpStatusCode.NotFound)] // 404
        [InlineData(HttpStatusCode.InternalServerError)] // 500
        [InlineData(HttpStatusCode.BadGateway)] // 502
        public async Task IsValidUrlAsync_ValidUrlWithErrorResponse_ShouldReturnFalse(HttpStatusCode statusCode)
        {
            // Arrange
            var url = "https://example.com";
            SetupHttpResponse(statusCode);

            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("invalid-url")]
        [InlineData("ftp://example.com")]
        public async Task IsValidUrlAsync_InvalidUrlFormat_ShouldReturnFalse(string url)
        {
            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsValidUrlAsync_HttpRequestException_ShouldReturnFalse()
        {
            // Arrange
            var url = "https://example.com";
            SetupHttpException(new HttpRequestException("Connection failed"));

            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.False(result);
            VerifyWarningLogged("URL validation failed for");
        }

        [Fact]
        public async Task IsValidUrlAsync_TaskCanceledException_ShouldReturnFalse()
        {
            // Arrange
            var url = "https://example.com";
            SetupHttpException(new TaskCanceledException("Request timeout"));

            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.False(result);
            VerifyWarningLogged("URL validation timeout for");
        }

        [Fact]
        public async Task IsValidUrlAsync_UnexpectedException_ShouldReturnFalse()
        {
            // Arrange
            var url = "https://example.com";
            SetupHttpException(new InvalidOperationException("Unexpected error"));

            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.False(result);
            VerifyErrorLogged("Unexpected error during URL validation for");
        }

        [Theory]
        [InlineData(HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.Created)]
        [InlineData(HttpStatusCode.Accepted)]
        [InlineData(HttpStatusCode.NoContent)]
        [InlineData(HttpStatusCode.MovedPermanently)]
        [InlineData(HttpStatusCode.Found)]
        [InlineData(HttpStatusCode.SeeOther)]
        [InlineData(HttpStatusCode.NotModified)]
        public async Task IsValidUrlAsync_SuccessAndRedirectCodes_ShouldReturnTrue(HttpStatusCode statusCode)
        {
            // Arrange
            var url = "https://example.com";
            SetupHttpResponse(statusCode);

            // Act
            var result = await _service.IsValidUrlAsync(url);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsValidUrlAsync_NullUrl_ShouldReturnFalse()
        {
            // Act
            var result = await _service.IsValidUrlAsync(null);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsValidUrlAsync_WhitespaceUrl_ShouldReturnFalse()
        {
            // Act
            var result = await _service.IsValidUrlAsync("   ");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsValidUrlFormat_UrlWithPort_ShouldReturnTrue()
        {
            // Arrange
            var url = "https://example.com:8080/path";

            // Act
            var result = _service.IsValidUrlFormat(url);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidUrlFormat_UrlWithQuery_ShouldReturnTrue()
        {
            // Arrange
            var url = "https://example.com/search?q=test&page=1";

            // Act
            var result = _service.IsValidUrlFormat(url);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsValidUrlFormat_UrlWithFragment_ShouldReturnTrue()
        {
            // Arrange
            var url = "https://example.com/page#section";

            // Act
            var result = _service.IsValidUrlFormat(url);

            // Assert
            Assert.True(result);
        }

        private void SetupHttpResponse(HttpStatusCode statusCode)
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(statusCode));
        }

        private void SetupHttpException(Exception exception)
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(exception);
        }

        private void VerifyWarningLogged(string messageContains)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        private void VerifyErrorLogged(string messageContains)
        {
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messageContains)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}