using System.Net;
using System.Text.Json;

namespace WeatherApi.Tests.Integration;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task GetHealth_ReturnsOkStatus()
    {
        await using WeatherApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Stream responseStream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        JsonDocument payload = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("ok", payload.RootElement.GetProperty("status").GetString());
    }
}
