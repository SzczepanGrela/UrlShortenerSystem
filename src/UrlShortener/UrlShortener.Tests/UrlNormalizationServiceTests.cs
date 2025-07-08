using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using UrlShortener.API.Services;

namespace UrlShortener.Tests;

public class UrlNormalizationServiceTests
{
    private readonly UrlNormalizationService _service;
    private readonly Mock<ILogger<UrlNormalizationService>> _mockLogger;

    public UrlNormalizationServiceTests()
    {
        _mockLogger = new Mock<ILogger<UrlNormalizationService>>();
        _service = new UrlNormalizationService(_mockLogger.Object);
    }

    [Theory]
    [InlineData("HTTP://EXAMPLE.COM", "http://example.com")]
    [InlineData("HTTPS://EXAMPLE.COM", "https://example.com")]
    [InlineData("Http://Example.Com", "http://example.com")]
    public void NormalizeUrl_ShouldLowercaseSchemeAndHost(string input, string expected)
    {
        // Act
        var result = _service.NormalizeUrl(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com:80", "http://example.com")]
    [InlineData("https://example.com:443", "https://example.com")]
    [InlineData("http://example.com:8080", "http://example.com:8080")]
    [InlineData("https://example.com:8443", "https://example.com:8443")]
    public void NormalizeUrl_ShouldRemoveDefaultPorts(string input, string expected)
    {
        // Act
        var result = _service.NormalizeUrl(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com/", "http://example.com")]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("http://example.com/path/", "http://example.com/path/")]
    public void NormalizeUrl_ShouldHandleTrailingSlash(string input, string expected)
    {
        // Act
        var result = _service.NormalizeUrl(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("http://example.com?param=value", "http://example.com?param=value")]
    [InlineData("http://example.com#fragment", "http://example.com#fragment")]
    [InlineData("http://example.com?param=value#fragment", "http://example.com?param=value#fragment")]
    public void NormalizeUrl_ShouldPreserveQueryAndFragment(string input, string expected)
    {
        // Act
        var result = _service.NormalizeUrl(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizeUrl_ShouldHandleInvalidInput(string input)
    {
        // Act
        var result = _service.NormalizeUrl(input);

        // Assert
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("invalid://url")]
    public void NormalizeUrl_ShouldReturnOriginalForInvalidUrls(string input)
    {
        // Act
        var result = _service.NormalizeUrl(input);

        // Assert
        Assert.Equal(input, result);
    }

    [Fact]
    public void NormalizeUrl_ComplexUrl_ShouldNormalizeCorrectly()
    {
        // Arrange
        var input = "HTTPS://EXAMPLE.COM:443/Path/To/Resource?param1=value1&param2=value2#section1";
        var expected = "https://example.com/Path/To/Resource?param1=value1&param2=value2#section1";

        // Act
        var result = _service.NormalizeUrl(input);

        // Assert
        Assert.Equal(expected, result);
    }
}