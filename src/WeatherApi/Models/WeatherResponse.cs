namespace WeatherApi.Models;

public sealed record WeatherResponse(
    string City,
    int TempC,
    string Condition,
    string Source,
    DateTimeOffset ObservedAt);
