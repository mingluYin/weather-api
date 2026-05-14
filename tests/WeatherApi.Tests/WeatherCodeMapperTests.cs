using WeatherApi.Services;

namespace WeatherApi.Tests;

public sealed class WeatherCodeMapperTests
{
    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(3, "Partly cloudy")]
    [InlineData(61, "Rain")]
    [InlineData(95, "Thunderstorm")]
    [InlineData(999, "Unknown")]
    public void ToCondition_MapsOpenMeteoWeatherCodes(int weatherCode, string expectedCondition)
    {
        string condition = WeatherCodeMapper.ToCondition(weatherCode);

        Assert.Equal(expectedCondition, condition);
    }
}
