using System.Diagnostics;
using System.Net.Http.Json;
using HashProcessing.Api.Application;

namespace HashProcessing.IntegrationTests;

public class EndToEndTests(IntegrationTestFixture fixture) : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));

    [Fact]
    public async Task GenerateHashesAndRetrieveProcessedCount()
    {
        using var httpClient = _fixture.ApiFactory.CreateClient();
        
        var initialResponse = await httpClient.GetAsync("/hashes");
        initialResponse.EnsureSuccessStatusCode();
        var initialHashesResponse = await initialResponse.Content.ReadFromJsonAsync<HashesResponse>();
        var initialTotalCount = initialHashesResponse!.Hashes.Sum(h => h.Count);
        
        var postResponse = await httpClient.PostAsync("/hashes", null);
        postResponse.EnsureSuccessStatusCode();
        
        // Poll until the processing pipeline completes or timeout
        var timeout = TimeSpan.FromSeconds(30);
        var pollInterval = TimeSpan.FromMilliseconds(500);
        var sw = Stopwatch.StartNew();
        var finalTotalCount = initialTotalCount;

        while (sw.Elapsed < timeout)
        {
            await Task.Delay(pollInterval);

            var response = await httpClient.GetAsync("/hashes");
            response.EnsureSuccessStatusCode();
            var hashesResponse = await response.Content.ReadFromJsonAsync<HashesResponse>();
            finalTotalCount = hashesResponse!.Hashes.Sum(h => h.Count);

            if (finalTotalCount > initialTotalCount)
                break;
        }

        Assert.True(finalTotalCount > initialTotalCount);
    }
}