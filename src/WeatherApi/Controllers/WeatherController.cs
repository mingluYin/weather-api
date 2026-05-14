using Microsoft.AspNetCore.Mvc;
using WeatherApi.Exceptions;
using WeatherApi.Models;
using WeatherApi.Services;

namespace WeatherApi.Controllers;

[ApiController]
[Route("weather")]
public sealed class WeatherController : ControllerBase
{
    private readonly ILogger<WeatherController> logger;
    private readonly IWeatherProvider weatherProvider;

    public WeatherController(
        IWeatherProvider weatherProvider,
        ILogger<WeatherController> logger)
    {
        this.weatherProvider = weatherProvider;
        this.logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(WeatherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WeatherResponse>> GetCurrentWeatherAsync(
        [FromQuery] string? city,
        CancellationToken cancellationToken)
    {
        // Reject empty input early so the provider can assume it receives a usable city.
        if (string.IsNullOrWhiteSpace(city))
        {
            logger.LogWarning("Rejected weather request with missing city");

            return Problem(
                title: "City is required",
                detail: "Provide a non-empty city query string, for example /weather?city=Sydney.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            WeatherResponse weather = await weatherProvider.GetCurrentWeatherAsync(city, cancellationToken);
            logger.LogInformation("Returned weather for {City} from {Source}", weather.City, weather.Source);

            return Ok(weather);
        }
        catch (CityNotFoundException exception)
        {
            logger.LogWarning(exception, "Could not find weather location for {City}", city);

            return Problem(
                title: "City was not found",
                detail: exception.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (WeatherProviderUnavailableException exception)
        {
            logger.LogWarning(exception, "Weather provider was unavailable for {City}", city);

            return Problem(
                title: "Weather provider unavailable",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
