using WeatherApi.Services;

namespace WeatherApi.Tests;

public sealed class MockWeatherProviderTests
{
    [Fact]
    public async Task GetCurrentWeatherAsync_ReturnsKnownSydneyWeather()
    {
        MockWeatherProvider provider = new();

        var result = await provider.GetCurrentWeatherAsync("Sydney", CancellationToken.None);

        Assert.Equal("Sydney", result.City);
        Assert.Equal(22, result.TempC);
        Assert.Equal("Sunny", result.Condition);
        Assert.Equal("mock", result.Source);
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_TrimsCityNames()
    {
        MockWeatherProvider provider = new();

        var result = await provider.GetCurrentWeatherAsync("  Perth  ", CancellationToken.None);

        Assert.Equal("Perth", result.City);
        Assert.Equal(24, result.TempC);
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_RejectsBlankCity()
    {
        MockWeatherProvider provider = new();

        await Assert.ThrowsAsync<ArgumentException>(
            () => provider.GetCurrentWeatherAsync(" ", CancellationToken.None));
    }
}
