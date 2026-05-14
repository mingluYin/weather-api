using WeatherApi.Models;

namespace WeatherApi.Services;

public sealed class MockWeatherProvider : IWeatherProvider
{
    // Known cities make demo responses predictable for reviewers and tests.
    private static readonly IReadOnlyDictionary<string, (int TempC, string Condition)> KnownWeather =
        new Dictionary<string, (int TempC, string Condition)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sydney"] = (22, "Sunny"),
            ["Melbourne"] = (18, "Cloudy"),
            ["Brisbane"] = (25, "Humid"),
            ["Perth"] = (24, "Clear"),
            ["Hobart"] = (14, "Cool")
        };

    private static readonly string[] Conditions =
    [
        "Sunny",
        "Cloudy",
        "Windy",
        "Showers",
        "Clear"
    ];

    public Task<WeatherResponse> GetCurrentWeatherAsync(string city, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        cancellationToken.ThrowIfCancellationRequested();

        string normalizedCity = city.Trim();

        // Return fixed data for common Australian cities so examples in the README
        // and automated tests remain deterministic.
        if (KnownWeather.TryGetValue(normalizedCity, out (int TempC, string Condition) known))
        {
            return Task.FromResult(CreateResponse(normalizedCity, known.TempC, known.Condition));
        }

        // Unknown cities still get stable mock weather based on the city name.
        // This avoids random output while keeping the endpoint useful for any input.
        uint seed = (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(normalizedCity);
        int tempC = 10 + (int)(seed % 22);
        string condition = Conditions[seed % (uint)Conditions.Length];

        return Task.FromResult(CreateResponse(normalizedCity, tempC, condition));
    }

    private static WeatherResponse CreateResponse(string city, int tempC, string condition)
    {
        return new WeatherResponse(
            City: city,
            TempC: tempC,
            Condition: condition,
            Source: "mock",
            ObservedAt: DateTimeOffset.UtcNow);
    }
}
