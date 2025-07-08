using Microsoft.AspNetCore.Mvc.Testing;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using UrlShortener.CrossCutting.Dtos;

namespace UrlShortener.Tests;

public class LoadTests : IClassFixture<TestWebApplicationFactory<Program>>, IDisposable
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public LoadTests(TestWebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _output = output;
    }

    [Fact]
    public async Task CreateLink_ConcurrentRequests_ShouldHandleLoad()
    {
        // Arrange
        const int concurrentRequests = 100;
        const int timeoutMs = 30000; // 30 seconds

        var tasks = new List<Task<(HttpStatusCode statusCode, TimeSpan duration)>>();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(CreateLinkWithTiming($"https://example.com/test{i}"));
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.statusCode == HttpStatusCode.OK || r.statusCode == HttpStatusCode.Created);
        var avgDuration = results.Where(r => r.statusCode == HttpStatusCode.OK || r.statusCode == HttpStatusCode.Created)
                                .Average(r => r.duration.TotalMilliseconds);
        var maxDuration = results.Max(r => r.duration.TotalMilliseconds);

        _output.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Successful requests: {successCount}/{concurrentRequests}");
        _output.WriteLine($"Average response time: {avgDuration:F2}ms");
        _output.WriteLine($"Max response time: {maxDuration:F2}ms");

        // At least 80% should succeed
        Assert.True(successCount >= concurrentRequests * 0.8, 
            $"Expected at least 80% success rate, got {successCount}/{concurrentRequests}");
        
        // Average response time should be reasonable
        Assert.True(avgDuration < 5000, $"Average response time too high: {avgDuration}ms");
        
        // Total time should be reasonable (should handle requests concurrently)
        Assert.True(stopwatch.ElapsedMilliseconds < timeoutMs, 
            $"Total time exceeded timeout: {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetLink_ConcurrentRequests_ShouldHandleLoad()
    {
        // Arrange - First create a link
        var createRequest = new CreateLinkRequestDto
        {
            OriginalUrl = "https://example.com/load-test",
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var createResponse = await CreateLink(createRequest);
        Assert.True(createResponse.IsSuccessStatusCode);

        var createContent = await createResponse.Content.ReadAsStringAsync();
        var linkData = JsonSerializer.Deserialize<JsonElement>(createContent);
        var shortCode = linkData.GetProperty("shortCode").GetString();
        
        Assert.NotNull(shortCode);

        const int concurrentRequests = 200;
        var tasks = new List<Task<(HttpStatusCode statusCode, TimeSpan duration)>>();

        // Act
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(GetLinkWithTiming(shortCode));
        }

        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        var successCount = results.Count(r => r.statusCode == HttpStatusCode.OK);
        var avgDuration = results.Where(r => r.statusCode == HttpStatusCode.OK)
                                .Average(r => r.duration.TotalMilliseconds);

        _output.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Successful requests: {successCount}/{concurrentRequests}");
        _output.WriteLine($"Average response time: {avgDuration:F2}ms");

        // All should succeed (link exists)
        Assert.Equal(concurrentRequests, successCount);
        
        // Should be fast due to caching
        Assert.True(avgDuration < 1000, $"Average response time too high: {avgDuration}ms");
    }

    [Fact]
    public async Task CreateLink_SustainedLoad_ShouldMaintainPerformance()
    {
        // Arrange
        const int requestsPerBatch = 20;
        const int numberOfBatches = 5;
        const int delayBetweenBatches = 1000; // 1 second

        var allResults = new List<(HttpStatusCode statusCode, TimeSpan duration)>();

        // Act
        for (int batch = 0; batch < numberOfBatches; batch++)
        {
            _output.WriteLine($"Starting batch {batch + 1}/{numberOfBatches}");
            
            var tasks = new List<Task<(HttpStatusCode statusCode, TimeSpan duration)>>();
            
            for (int i = 0; i < requestsPerBatch; i++)
            {
                var url = $"https://example.com/sustained-test-batch{batch}-req{i}";
                tasks.Add(CreateLinkWithTiming(url));
            }

            var batchResults = await Task.WhenAll(tasks);
            allResults.AddRange(batchResults);

            var batchSuccessCount = batchResults.Count(r => r.statusCode == HttpStatusCode.OK || r.statusCode == HttpStatusCode.Created);
            var batchAvgDuration = batchResults.Where(r => r.statusCode == HttpStatusCode.OK || r.statusCode == HttpStatusCode.Created)
                                              .Average(r => r.duration.TotalMilliseconds);

            _output.WriteLine($"Batch {batch + 1} - Success: {batchSuccessCount}/{requestsPerBatch}, Avg time: {batchAvgDuration:F2}ms");

            if (batch < numberOfBatches - 1)
            {
                await Task.Delay(delayBetweenBatches);
            }
        }

        // Assert
        var totalRequests = requestsPerBatch * numberOfBatches;
        var totalSuccessCount = allResults.Count(r => r.statusCode == HttpStatusCode.OK || r.statusCode == HttpStatusCode.Created);
        var overallAvgDuration = allResults.Where(r => r.statusCode == HttpStatusCode.OK || r.statusCode == HttpStatusCode.Created)
                                          .Average(r => r.duration.TotalMilliseconds);

        _output.WriteLine($"Overall - Success: {totalSuccessCount}/{totalRequests}, Avg time: {overallAvgDuration:F2}ms");

        // At least 90% should succeed under sustained load
        Assert.True(totalSuccessCount >= totalRequests * 0.9, 
            $"Expected at least 90% success rate under sustained load, got {totalSuccessCount}/{totalRequests}");
        
        // Performance should remain consistent
        Assert.True(overallAvgDuration < 3000, $"Average response time degraded: {overallAvgDuration}ms");
    }

    [Fact]
    public async Task MemoryUsage_UnderLoad_ShouldNotLeak()
    {
        // Arrange
        const int iterations = 50;
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var tasks = new List<Task>();
            
            for (int j = 0; j < 10; j++)
            {
                tasks.Add(CreateLink(new CreateLinkRequestDto
                {
                    OriginalUrl = $"https://example.com/memory-test-{i}-{j}",
                    ExpirationDate = DateTime.UtcNow.AddDays(1)
                }));
            }

            await Task.WhenAll(tasks);

            // Force garbage collection every 10 iterations
            if (i % 10 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Final cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        _output.WriteLine($"Initial memory: {initialMemory:N0} bytes");
        _output.WriteLine($"Final memory: {finalMemory:N0} bytes");
        _output.WriteLine($"Memory increase: {memoryIncrease:N0} bytes");

        // Assert - Memory increase should be reasonable (less than 10MB)
        Assert.True(memoryIncrease < 10 * 1024 * 1024, 
            $"Potential memory leak detected. Memory increased by {memoryIncrease:N0} bytes");
    }

    private async Task<(HttpStatusCode statusCode, TimeSpan duration)> CreateLinkWithTiming(string url)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var request = new CreateLinkRequestDto
        {
            OriginalUrl = url,
            ExpirationDate = DateTime.UtcNow.AddDays(1)
        };

        var response = await CreateLink(request);
        stopwatch.Stop();

        return (response.StatusCode, stopwatch.Elapsed);
    }

    private async Task<(HttpStatusCode statusCode, TimeSpan duration)> GetLinkWithTiming(string shortCode)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await _client.GetAsync($"/api/links/{shortCode}");
        stopwatch.Stop();

        return (response.StatusCode, stopwatch.Elapsed);
    }

    private async Task<HttpResponseMessage> CreateLink(CreateLinkRequestDto request)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/api/links", content);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}