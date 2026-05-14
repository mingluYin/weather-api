using WeatherApi.Exceptions;

namespace WeatherApi.Tests;

public sealed class WeatherProviderUnavailableExceptionTests
{
    [Fact]
    public void Constructor_IncludesProviderNameAndOfflineGuidance()
    {
        TimeoutException innerException = new("Timed out");

        WeatherProviderUnavailableException exception = new("Open-Meteo", innerException);

        Assert.Contains("Open-Meteo is currently unavailable", exception.Message);
        Assert.Contains("Mock provider", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }
}
