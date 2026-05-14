using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WeatherApi.Controllers;
using WeatherApi.Models;
using WeatherApi.Services;

namespace WeatherApi.Tests;

public sealed class WeatherControllerTests
{
    [Fact]
    public async Task GetCurrentWeatherAsync_ReturnsSuccessResponse()
    {
        WeatherResponse weather = new(
            City: "Sydney",
            TempC: 22,
            Condition: "Sunny",
            Source: "test",
            ObservedAt: DateTimeOffset.UtcNow);

        WeatherController controller = new(
            new StubWeatherProvider(weather),
            NullLogger<WeatherController>.Instance);

        ActionResult<WeatherResponse> result =
            await controller.GetCurrentWeatherAsync("Sydney", CancellationToken.None);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
        WeatherResponse response = Assert.IsType<WeatherResponse>(okResult.Value);

        Assert.Equal(weather, response);
    }

    [Fact]
    public async Task GetCurrentWeatherAsync_ReturnsBadRequestProblemDetailsForMissingCity()
    {
        WeatherController controller = new(
            new StubWeatherProvider(null),
            NullLogger<WeatherController>.Instance);

        ActionResult<WeatherResponse> result =
            await controller.GetCurrentWeatherAsync(" ", CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result.Result);
        ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.Equal("City is required", problemDetails.Title);
    }

    private sealed class StubWeatherProvider : IWeatherProvider
    {
        private readonly WeatherResponse? weather;

        public StubWeatherProvider(WeatherResponse? weather)
        {
            this.weather = weather;
        }

        public Task<WeatherResponse> GetCurrentWeatherAsync(
            string city,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(weather ?? throw new InvalidOperationException("No weather configured."));
        }
    }
}
