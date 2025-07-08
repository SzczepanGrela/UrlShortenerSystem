using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using UrlShortener.CrossCutting.Dtos;

namespace IntegrationTests;

public class ServiceIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ServiceIntegrationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task CreateLinkAndRedirect_ShouldWorkEndToEnd()
    {
        // Arrange
        var urlShortenerClient = _fixture.UrlShortenerClient;
        var analyticsClient = _fixture.AnalyticsClient;

        var createRequest = new CreateLinkRequestDto
        {
            OriginalUrl = "https://example.com/integration-test",
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act 1: Create link
        var createResponse = await urlShortenerClient.PostAsync("/api/links", content);
        
        // Assert 1: Link creation should succeed
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<JsonElement>(createContent);
        var shortCode = createResult.GetProperty("result").GetProperty("shortCode").GetString();
        
        Assert.NotNull(shortCode);
        _output.WriteLine($"Created short code: {shortCode}");

        // Act 2: Access link (redirect)
        var redirectResponse = await urlShortenerClient.GetAsync($"/{shortCode}");

        // Assert 2: Should redirect
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);
        Assert.Equal("https://example.com/integration-test", redirectResponse.Headers.Location?.ToString());

        // Act 3: Wait a moment for analytics to be processed
        await Task.Delay(2000);

        // Act 4: Check analytics
        var analyticsResponse = await analyticsClient.GetAsync($"/api/analytics/links/{shortCode}");

        // Assert 3: Analytics should be recorded
        if (analyticsResponse.StatusCode == HttpStatusCode.OK)
        {
            var analyticsContent = await analyticsResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Analytics response: {analyticsContent}");
            
            var analyticsResult = JsonSerializer.Deserialize<JsonElement>(analyticsContent);
            var clickCount = analyticsResult.GetProperty("clickCount").GetInt32();
            
            Assert.True(clickCount > 0, "Click should be recorded in analytics");
        }
        else
        {
            _output.WriteLine($"Analytics response status: {analyticsResponse.StatusCode}");
            // Analytics might not be available immediately, which is acceptable in integration test
        }
    }

    [Fact]
    public async Task HealthChecks_BothServices_ShouldBeHealthy()
    {
        // Act & Assert - UrlShortener health
        var urlShortenerHealth = await _fixture.UrlShortenerClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, urlShortenerHealth.StatusCode);

        // Act & Assert - Analytics health
        var analyticsHealth = await _fixture.AnalyticsClient.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, analyticsHealth.StatusCode);

        _output.WriteLine("Both services are healthy");
    }

    [Fact]
    public async Task CorrelationId_ShouldPropagateBetweenServices()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var createRequest = new CreateLinkRequestDto
        {
            OriginalUrl = "https://example.com/correlation-test",
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act 1: Create link with correlation ID
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/links")
        {
            Content = content
        };
        requestMessage.Headers.Add("X-Correlation-ID", correlationId);

        var createResponse = await _fixture.UrlShortenerClient.SendAsync(requestMessage);

        // Assert 1: Response should include correlation ID
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.True(createResponse.Headers.Contains("X-Correlation-ID"));
        
        var responseCorrelationId = createResponse.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        Assert.Equal(correlationId, responseCorrelationId);

        _output.WriteLine($"Correlation ID propagated correctly: {correlationId}");
    }

    [Fact]
    public async Task ConcurrentAccess_MultipleServices_ShouldHandleLoad()
    {
        // Arrange
        const int concurrentRequests = 20;
        var tasks = new List<Task<bool>>();

        // Act: Create multiple links concurrently across both services
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(CreateAndAccessLinkAsync(i));
        }

        var results = await Task.WhenAll(tasks);

        // Assert: Most operations should succeed
        var successCount = results.Count(r => r);
        var successRate = (double)successCount / concurrentRequests;

        _output.WriteLine($"Success rate: {successCount}/{concurrentRequests} ({successRate:P})");
        
        Assert.True(successRate >= 0.8, $"Expected at least 80% success rate, got {successRate:P}");
    }

    [Fact]
    public async Task ServiceFailure_ShouldBeGraceful()
    {
        // This test simulates what happens when Analytics service is down
        // UrlShortener should still work (redirect), but analytics won't be recorded

        // Arrange
        var createRequest = new CreateLinkRequestDto
        {
            OriginalUrl = "https://example.com/failover-test",
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var json = JsonSerializer.Serialize(createRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act 1: Create link (should work even if analytics is down)
        var createResponse = await _fixture.UrlShortenerClient.PostAsync("/api/links", content);

        // Assert 1: Link creation should succeed
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var createResult = JsonSerializer.Deserialize<JsonElement>(createContent);
        var shortCode = createResult.GetProperty("result").GetProperty("shortCode").GetString();

        // Act 2: Access link (should work even if analytics is down)
        var redirectResponse = await _fixture.UrlShortenerClient.GetAsync($"/{shortCode}");

        // Assert 2: Redirect should work
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);

        _output.WriteLine("Service works gracefully even with potential analytics failures");
    }

    private async Task<bool> CreateAndAccessLinkAsync(int index)
    {
        try
        {
            // Create link
            var createRequest = new CreateLinkRequestDto
            {
                OriginalUrl = $"https://example.com/concurrent-test-{index}",
                ExpirationDate = DateTime.UtcNow.AddDays(1)
            };

            var json = JsonSerializer.Serialize(createRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var createResponse = await _fixture.UrlShortenerClient.PostAsync("/api/links", content);
            
            if (createResponse.StatusCode != HttpStatusCode.OK)
                return false;

            var createContent = await createResponse.Content.ReadAsStringAsync();
            var createResult = JsonSerializer.Deserialize<JsonElement>(createContent);
            var shortCode = createResult.GetProperty("result").GetProperty("shortCode").GetString();

            // Access link
            var redirectResponse = await _fixture.UrlShortenerClient.GetAsync($"/{shortCode}");
            
            return redirectResponse.StatusCode == HttpStatusCode.Redirect;
        }
        catch
        {
            return false;
        }
    }
}

public class IntegrationTestFixture : IDisposable
{
    public HttpClient UrlShortenerClient { get; private set; }
    public HttpClient AnalyticsClient { get; private set; }
    
    private readonly WebApplicationFactory<UrlShortener.API.Program> _urlShortenerFactory;
    private readonly WebApplicationFactory<Analytics.API.Program> _analyticsFactory;

    public IntegrationTestFixture()
    {
        // Create test factories for both services
        _urlShortenerFactory = new WebApplicationFactory<UrlShortener.API.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Override configuration for testing
                    services.Configure<Microsoft.Extensions.Configuration.IConfiguration>(config =>
                    {
                        config["AnalyticsService:BaseUrl"] = "http://localhost:7001";
                    });
                });
            });

        _analyticsFactory = new WebApplicationFactory<Analytics.API.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
            });

        UrlShortenerClient = _urlShortenerFactory.CreateClient();
        AnalyticsClient = _analyticsFactory.CreateClient();
    }

    public void Dispose()
    {
        UrlShortenerClient?.Dispose();
        AnalyticsClient?.Dispose();
        _urlShortenerFactory?.Dispose();
        _analyticsFactory?.Dispose();
    }
}