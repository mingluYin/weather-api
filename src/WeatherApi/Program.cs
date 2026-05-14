using Amazon.Lambda.AspNetCoreServer.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Scalar.AspNetCore;
using WeatherApi.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();

// OpenAPI powers the Scalar UI used during local development.
builder.Services.AddOpenApi();

// This keeps local development simple while allowing the same app to run behind
// API Gateway HTTP API when packaged and deployed to AWS Lambda.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

string weatherProvider = builder.Configuration.GetValue("Weather:Provider", "OpenMeteo")!;

// Register the weather provider behind an interface. The implementation is
// selected from configuration so local demos can use Mock while production
// can use Open-Meteo without changing code.
if (string.Equals(weatherProvider, "Mock", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IWeatherProvider, MockWeatherProvider>();
}
else
{
    builder.Services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(
        client => client.Timeout = TimeSpan.FromSeconds(10));
}

WebApplication app = builder.Build();
app.Logger.LogInformation("Weather provider configured as {WeatherProvider}", weatherProvider);

// Central exception handling prevents stack traces leaking to clients and
// ensures unexpected failures are logged for operations support.
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        IExceptionHandlerFeature? feature = context.Features.Get<IExceptionHandlerFeature>();
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        logger.LogError(feature?.Error, "Unhandled request failure");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            title: "Unexpected server error",
            detail: "The request could not be completed.",
            statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    });
});

if (!app.Environment.IsProduction())
{
    // Keep interactive API documentation available for local dev/test profiles.
    // Production can expose OpenAPI later behind authentication if the team needs it.
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Weather API");
    });
}

app.MapGet("/", () => Results.Redirect(!app.Environment.IsProduction() ? "/scalar/v1" : "/health"))
    .WithName("Root")
    .WithSummary("Redirects to the most useful local or production entry point.");

app.MapControllers();

app.Run();

public partial class Program
{
}
