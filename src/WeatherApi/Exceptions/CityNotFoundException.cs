namespace WeatherApi.Exceptions;

public sealed class CityNotFoundException : Exception
{
    public CityNotFoundException(string city)
        : base($"No Open-Meteo location match was found for '{city}'.")
    {
    }
}
