using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WeatherApi.Models;
using WeatherApi.Services;

namespace WeatherApi.Tests.Integration;

internal sealed class WeatherApiFactory : WebApplicationFactory<Program>
{
    private readonly IWeatherProvider weatherProvider;

    public WeatherApiFactory(IWeatherProvider? weatherProvider = null)
    {
        this.weatherProvider = weatherProvider ?? new FixedWeatherProvider();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Replace the real external provider so integration tests exercise
            // ASP.NET routing, DI, middleware, and JSON without calling Open-Meteo.
            services.RemoveAll<IWeatherProvider>();
            services.AddSingleton(weatherProvider);
        });
    }

    private sealed class FixedWeatherProvider : IWeatherProvider
    {
        public Task<WeatherResponse> GetCurrentWeatherAsync(
            string city,
            CancellationToken cancellationToken)
        {
            WeatherResponse weather = new(
                City: $"{city.Trim()}, Australia",
                TempC: 22,
                Condition: "Clear sky",
                Source: "integration-test",
                ObservedAt: DateTimeOffset.Parse("2026-05-12T10:30:00+00:00"));

            return Task.FromResult(weather);
        }
    }
}
