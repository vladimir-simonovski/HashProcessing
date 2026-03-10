namespace HashProcessing.IntegrationTests;

public class EndToEndTests(ApiWebApplicationFactory apiWebApplicationFactory)
    : IClassFixture<WorkerApplicationFactory>, IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory
        _apiWebApplicationFactory = apiWebApplicationFactory
                                    ?? throw new ArgumentNullException(nameof(apiWebApplicationFactory));

    [Fact]
    public async Task GenerateHashesAndRetrieveCount()
    {
        using var httpClient = _apiWebApplicationFactory.CreateClient();
        await httpClient.PostAsync("/hashes", null);
        
        await Task.Delay(TimeSpan.FromSeconds(2)); // Wait for processing
        
        var response = await httpClient.GetAsync("/hashes");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
    }
}