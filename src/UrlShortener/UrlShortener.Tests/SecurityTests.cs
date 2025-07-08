using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using UrlShortener.CrossCutting.Dtos;

namespace UrlShortener.Tests;

public class SecurityTests : IClassFixture<TestWebApplicationFactory<Program>>, IDisposable
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SecurityTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData("javascript:alert('xss')")]
    [InlineData("data:text/html,<script>alert('xss')</script>")]
    [InlineData("vbscript:msgbox('xss')")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://malicious.com/payload")]
    public async Task CreateLink_MaliciousUrls_ShouldReject(string maliciousUrl)
    {
        // Arrange
        var request = new CreateLinkRequestDto
        {
            OriginalUrl = maliciousUrl,
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/links", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://172.16.0.1/")]
    public async Task CreateLink_LocalNetworkUrls_ShouldReject(string localUrl)
    {
        // Arrange
        var request = new CreateLinkRequestDto
        {
            OriginalUrl = localUrl,
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/links", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLink_ExtremelyLongUrl_ShouldReject()
    {
        // Arrange - URL longer than 2048 characters
        var longUrl = "https://example.com/" + new string('a', 2500);
        var request = new CreateLinkRequestDto
        {
            OriginalUrl = longUrl,
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/links", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLink_SqlInjectionInUrl_ShouldReject()
    {
        // Arrange
        var maliciousUrl = "https://example.com/'; DROP TABLE Links; --";
        var request = new CreateLinkRequestDto
        {
            OriginalUrl = maliciousUrl,
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/links", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RateLimiting_ExcessiveRequests_ShouldThrottle()
    {
        // Arrange
        var validRequest = new CreateLinkRequestDto
        {
            OriginalUrl = "https://example.com",
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(validRequest);

        // Act - Make multiple requests sequentially with proper IP simulation
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 5; i++) // 5 requests against limit of 2 per minute
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/links")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            
            // Add both headers to ensure IP detection works
            request.Headers.Add("X-Forwarded-For", "203.0.113.100"); // Use RFC 5737 test IP
            request.Headers.Add("X-Real-IP", "203.0.113.100");
            
            var response = await _client.SendAsync(request);
            responses.Add(response);
            
            // Small delay to ensure proper timing
            if (i < 4) await Task.Delay(50);
        }

        // Assert - At least some requests should be rate limited (limit is 2 per minute)
        var successfulResponses = responses.Count(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.OK);
        var rateLimitedResponses = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        
        // Debug output
        var statusCodes = string.Join(", ", responses.Select(r => r.StatusCode));
        
        Assert.True(rateLimitedResponses > 0, $"Rate limiting should kick in for excessive requests. Expected some TooManyRequests (429) but got: {statusCodes}. Successful: {successfulResponses}, Rate limited: {rateLimitedResponses}");
        
        // Dispose responses
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("..\\..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("../../../proc/self/environ")]
    public async Task GetLink_PathTraversalAttempts_ShouldReject(string maliciousPath)
    {
        // Act
        var response = await _client.GetAsync($"/api/links/{maliciousPath}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("onload=alert('xss')")]
    [InlineData("'><script>alert('xss')</script>")]
    public async Task GetLink_XssAttempts_ShouldReject(string xssPayload)
    {
        // Act
        var response = await _client.GetAsync($"/api/links/{xssPayload}");

        // Assert - XSS payloads should be rejected with BadRequest due to input validation
        // or NotFound if they pass validation but don't exist
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected BadRequest or NotFound, but got {response.StatusCode}");
    }

    [Fact]
    public async Task Redirect_NonexistentShortCode_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/nonexistent");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("undefined")]
    public async Task CreateLink_EmptyOrInvalidUrl_ShouldReject(string invalidUrl)
    {
        // Arrange
        var request = new CreateLinkRequestDto
        {
            OriginalUrl = invalidUrl,
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/links", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateLink_MalformedJson_ShouldReject()
    {
        // Arrange
        var malformedJson = "{ \"OriginalUrl\": \"https://example.com\", \"ExpirationDate\": }";
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/links", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HealthCheck_ShouldNotLeakSystemInfo()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        
        // Should not contain sensitive system information
        Assert.DoesNotContain("password", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("key", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", content, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}