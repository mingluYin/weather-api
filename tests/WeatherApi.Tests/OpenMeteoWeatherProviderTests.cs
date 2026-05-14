using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherApi.Exceptions;
using WeatherApi.Services;

namespace WeatherApi.Tests;

public sealed class OpenMeteoWeatherProviderTests
{
    [Fact]
    public async Task GetCurrentWeatherAsync_MapsSuccessfulOpenMeteoResponses()
    {
        QueueHttpMessageHandler handler = new(
            JsonResponse("""{"results":[{"name":"Sydney","latitude":-33.8678,"longitude":151.2073,"country":"Australia"}]}"""),
            JsonResponse("""{"current":{"time":"2026-05-12T10:30","temperature_2m":21.6,"weather_code":0}}"""));

        OpenMeteoWeatherProvider provider = CreateProvider(handler);

        var weather = await provider.GetCurrentWeatherAsync("Sydney", CancellationToken.None);

        Assert.Equal("Sydney, Australia", weather.City);
        Assert.Equal(22, weather.TempC);
        Assert.Equal("Clear sky", weather.Condition);
        Assert.Equal("open-meteo", weather.Source);
        Assert.Equal(DateTimeOffset.Parse("2026-05-12T10:30:00+00:00"), weather.ObservedAt);
        Assert.Collection(
            handler.Requests,
            request => Assert.Contains("/v1/search?name=Sydney", request.RequestUri!.PathAndQuery),
            request => Assert.Contains("/v1/forecast?latitude=-33.8678&longitude=151.2073", request.RequestUri!.PathAndQuery));
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ThrowsCityNotFoundWhenGeocodingHasNoResults()
    {
        QueueHttpMessageHandler handler = new(JsonResponse("""{"results":[]}"""));
        OpenMeteoWeatherProvider provider = CreateProvider(handler);

        CityNotFoundException exception = await Assert.ThrowsAsync<CityNotFoundException>(
            () => provider.GetCurrentWeatherAsync("Atlantis", CancellationToken.None));

        Assert.Contains("Atlantis", exception.Message);
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ConvertsForecastHttpFailureToProviderUnavailable()
    {
        QueueHttpMessageHandler handler = new(
            JsonResponse("""{"results":[{"name":"Sydney","latitude":-33.8678,"longitude":151.2073,"country":"Australia"}]}"""),
            new HttpResponseMessage(HttpStatusCode.BadGateway));

        OpenMeteoWeatherProvider provider = CreateProvider(handler);

        await Assert.ThrowsAsync<WeatherProviderUnavailableException>(
            () => provider.GetCurrentWeatherAsync("Sydney", CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ConvertsBadJsonToProviderUnavailable()
    {
        QueueHttpMessageHandler handler = new(TextResponse("not-json"));
        OpenMeteoWeatherProvider provider = CreateProvider(handler);

        await Assert.ThrowsAsync<WeatherProviderUnavailableException>(
            () => provider.GetCurrentWeatherAsync("Sydney", CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ConvertsNetworkFailureToProviderUnavailable()
    {
        QueueHttpMessageHandler handler = new(new HttpRequestException("Network failure"));
        OpenMeteoWeatherProvider provider = CreateProvider(handler);

        await Assert.ThrowsAsync<WeatherProviderUnavailableException>(
            () => provider.GetCurrentWeatherAsync("Sydney", CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ConvertsTimeoutToProviderUnavailable()
    {
        QueueHttpMessageHandler handler = new(new TaskCanceledException("Request timed out"));
        OpenMeteoWeatherProvider provider = CreateProvider(handler);

        await Assert.ThrowsAsync<WeatherProviderUnavailableException>(
            () => provider.GetCurrentWeatherAsync("Sydney", CancellationToken.None));
    }

    private static OpenMeteoWeatherProvider CreateProvider(HttpMessageHandler handler)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:ApiUrl:GeocodingBaseUrl"] = "https://mock-open-meteo-geocoding.test",
                ["Weather:ApiUrl:ForecastBaseUrl"] = "https://mock-open-meteo-forecast.test"
            })
            .Build();

        return new OpenMeteoWeatherProvider(
            new HttpClient(handler),
            configuration,
            NullLogger<OpenMeteoWeatherProvider>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return TextResponse(json, "application/json");
    }

    private static HttpResponseMessage TextResponse(
        string content,
        string mediaType = "text/plain")
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType)
        };
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<object> responses;

        public QueueHttpMessageHandler(params object[] responses)
        {
            this.responses = new Queue<object>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            object response = responses.Dequeue();

            if (response is Exception exception)
            {
                return Task.FromException<HttpResponseMessage>(exception);
            }

            return Task.FromResult((HttpResponseMessage)response);
        }
    }
}
