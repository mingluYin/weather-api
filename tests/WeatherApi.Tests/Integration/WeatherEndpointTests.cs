using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WeatherApi.Exceptions;
using WeatherApi.Models;
using WeatherApi.Services;

namespace WeatherApi.Tests.Integration;

public sealed class WeatherEndpointTests
{
    [Fact]
    public async Task GetWeather_ReturnsExpectedContract()
    {
        await using WeatherApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/weather?city=Sydney",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        WeatherResponse? weather = await response.Content.ReadFromJsonAsync<WeatherResponse>(
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(weather);
        Assert.Equal("Sydney, Australia", weather.City);
        Assert.Equal(22, weather.TempC);
        Assert.Equal("Clear sky", weather.Condition);
        Assert.Equal("integration-test", weather.Source);
        Assert.Equal(DateTimeOffset.Parse("2026-05-12T10:30:00+00:00"), weather.ObservedAt);
    }

    [Theory]
    [InlineData("/weather")]
    [InlineData("/weather?city=")]
    [InlineData("/weather?city=%20%20")]
    public async Task GetWeather_ReturnsProblemDetailsForMissingOrBlankCity(string requestUri)
    {
        await using WeatherApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            requestUri,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertProblemDetailsAsync(response, "City is required", 400);
    }

    [Fact]
    public async Task GetWeather_ReturnsProblemDetailsWhenCityIsNotFound()
    {
        await using WeatherApiFactory factory = new(new ThrowingWeatherProvider(new CityNotFoundException("Atlantis")));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/weather?city=Atlantis",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertProblemDetailsAsync(response, "City was not found", 404);
    }

    [Fact]
    public async Task GetWeather_ReturnsProblemDetailsWhenProviderIsUnavailable()
    {
        WeatherProviderUnavailableException exception = new(
            "Open-Meteo",
            new TimeoutException("Timed out"));

        await using WeatherApiFactory factory = new(new ThrowingWeatherProvider(exception));
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/weather?city=Sydney",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await AssertProblemDetailsAsync(response, "Weather provider unavailable", 503);
    }

    private static async Task AssertProblemDetailsAsync(
        HttpResponseMessage response,
        string expectedTitle,
        int expectedStatus)
    {
        Stream responseStream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        JsonDocument problem = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedTitle, problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        Assert.True(problem.RootElement.TryGetProperty("detail", out JsonElement detail));
        Assert.False(string.IsNullOrWhiteSpace(detail.GetString()));
    }

    private sealed class ThrowingWeatherProvider : IWeatherProvider
    {
        private readonly Exception exception;

        public ThrowingWeatherProvider(Exception exception)
        {
            this.exception = exception;
        }

        public Task<WeatherResponse> GetCurrentWeatherAsync(
            string city,
            CancellationToken cancellationToken)
        {
            return Task.FromException<WeatherResponse>(exception);
        }
    }
}
