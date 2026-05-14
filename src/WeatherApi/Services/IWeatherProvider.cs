using WeatherApi.Models;

namespace WeatherApi.Services;

public interface IWeatherProvider
{
    Task<WeatherResponse> GetCurrentWeatherAsync(string city, CancellationToken cancellationToken);
}
