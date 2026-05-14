namespace WeatherApi.Exceptions;

public sealed class WeatherProviderUnavailableException : Exception
{
    public WeatherProviderUnavailableException(string providerName, Exception innerException)
        : base($"{providerName} is currently unavailable. Try again later or switch to the Mock provider for offline local testing.", innerException)
    {
    }
}
