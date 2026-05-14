using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WeatherApi.Exceptions;
using WeatherApi.Models;

namespace WeatherApi.Services;

public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<OpenMeteoWeatherProvider> logger;

    public OpenMeteoWeatherProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenMeteoWeatherProvider> logger)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task<WeatherResponse> GetCurrentWeatherAsync(string city, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string normalizedCity = city.Trim();
            OpenMeteoLocation location = await ResolveLocationAsync(normalizedCity, cancellationToken);
            OpenMeteoCurrentWeather current = await GetOpenMeteoCurrentWeatherAsync(location, cancellationToken);

            logger.LogInformation(
                "Resolved {RequestedCity} to {ResolvedCity} at {Latitude},{Longitude}",
                normalizedCity,
                location.Name,
                location.Latitude,
                location.Longitude);

            return new WeatherResponse(
                City: FormatLocation(location),
                TempC: (int)Math.Round(current.TemperatureC),
                Condition: WeatherCodeMapper.ToCondition(current.WeatherCode),
                Source: "open-meteo",
                ObservedAt: ParseObservedAt(current.Time));
        }
        catch (CityNotFoundException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherProviderUnavailableException("Open-Meteo", new TimeoutException("Open-Meteo did not respond before the configured timeout."));
        }
        catch (HttpRequestException exception)
        {
            throw new WeatherProviderUnavailableException("Open-Meteo", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherProviderUnavailableException("Open-Meteo", exception);
        }
    }

    private async Task<OpenMeteoLocation> ResolveLocationAsync(
        string city,
        CancellationToken cancellationToken)
    {
        string pathAndQuery = "/v1/search"
            + $"?name={Uri.EscapeDataString(city)}"
            + "&count=1"
            + "&language=en"
            + "&format=json";

        GeocodingResponse? response = await httpClient.GetFromJsonAsync<GeocodingResponse>(
            new Uri(GetRequiredUri("Weather:ApiUrl:GeocodingBaseUrl"), pathAndQuery),
            cancellationToken);

        OpenMeteoLocation? location = response?.Results?.FirstOrDefault();

        if (location is null)
        {
            throw new CityNotFoundException(city);
        }

        return location;
    }

    private async Task<OpenMeteoCurrentWeather> GetOpenMeteoCurrentWeatherAsync(
        OpenMeteoLocation location,
        CancellationToken cancellationToken)
    {
        string latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
        string longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);

        string pathAndQuery = "/v1/forecast"
            + $"?latitude={latitude}"
            + $"&longitude={longitude}"
            + "&current=temperature_2m,weather_code";

        ForecastResponse? response = await httpClient.GetFromJsonAsync<ForecastResponse>(
            new Uri(GetRequiredUri("Weather:ApiUrl:ForecastBaseUrl"), pathAndQuery),
            cancellationToken);

        if (response?.Current is null)
        {
            throw new HttpRequestException("Open-Meteo did not return current weather data.");
        }

        return response.Current;
    }

    private Uri GetRequiredUri(string configurationKey)
    {
        string? value = configuration[configurationKey];

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException($"Missing or invalid configuration value: {configurationKey}");
        }

        return uri;
    }

    private static string FormatLocation(OpenMeteoLocation location)
    {
        if (string.IsNullOrWhiteSpace(location.Country))
        {
            return location.Name;
        }

        return $"{location.Name}, {location.Country}";
    }

    private static DateTimeOffset ParseObservedAt(string time)
    {
        if (DateTimeOffset.TryParse(
            time,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset observedAt))
        {
            return observedAt;
        }

        return DateTimeOffset.UtcNow;
    }

    private sealed record GeocodingResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<OpenMeteoLocation>? Results);

    private sealed record OpenMeteoLocation(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude,
        [property: JsonPropertyName("country")] string? Country);

    private sealed record ForecastResponse(
        [property: JsonPropertyName("current")] OpenMeteoCurrentWeather? Current);

    private sealed record OpenMeteoCurrentWeather(
        [property: JsonPropertyName("time")] string Time,
        [property: JsonPropertyName("temperature_2m")] double TemperatureC,
        [property: JsonPropertyName("weather_code")] int WeatherCode);
}
