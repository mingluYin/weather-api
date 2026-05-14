# Weather API technical test

Small ASP.NET Core Web API written for a Senior DevOps technical test. It returns current weather for a city using the free Open-Meteo API and includes the delivery workflow pieces reviewers asked for: tests, linting, CI packaging, and Terraform IaC for AWS.

## Design choices

- **Runtime:** .NET 10 because it is the current LTS .NET release and AWS Lambda supports the managed `dotnet10` runtime.
- **Hosting model:** ASP.NET Core Web API controllers with `Amazon.Lambda.AspNetCoreServer.Hosting`, so the same app runs locally on Kestrel and can run behind API Gateway in Lambda.
- **No Docker Desktop:** The workflow uses normal `dotnet publish` output zipped for Lambda. No container build is required.
- **No AWS account required:** Terraform is included as a deployable design, but CI only validates it.
- **Weather data:** The provider is configurable. The dev profile uses `Mock` for offline demos and deterministic responses. The test profile uses `OpenMeteo` because it is free for non-commercial use and does not require an API key.
- **API docs:** Scalar is enabled for non-production profiles at `/scalar/v1`, backed by the OpenAPI endpoint at `/openapi/v1.json`.
- **Quality gates:** GitHub Actions restores with cache, runs `dotnet format --verify-no-changes`, builds with warnings as errors, tests, publishes, zips, uploads an artifact, and validates Terraform.
- **Bonus items:** CI linting, CloudWatch logging and alarms, GitHub Actions test to prod deployment placeholders, and a Python response-contract validation helper are included.

## Architecture

```text
weather-api/
|-- src/
|   `-- WeatherApi/                         # ASP.NET Core Web API (.NET 10)
|-- tests/
|   `-- WeatherApi.Tests/                    # Unit tests
|-- infra/
|   `-- terraform/                           # AWS Lambda/API Gateway IaC
|-- scripts/
|   `-- validate_weather_response.py         # Response contract validation helper
|-- samples/
|   `-- weather-response.json                # Sample response for Python validation
|-- .github/
|   `-- workflows/                           # CI, deploy-test, deploy-prod workflows
`-- README.md
```

## Dependency flow

```text
                         WeatherApi
           Controllers + Models + Services + Program
                              |
                              v
                      IWeatherProvider
                              |
              +---------------+----------------+
              |                                |
              v                                v
     MockWeatherProvider            OpenMeteoWeatherProvider
              |                                |
              |                                v
              |                    Weather:ApiUrl config values
              |                                |
              |                                v
              |                    Open-Meteo Geocoding API
              |                                |
              |                                v
              |                    Open-Meteo Forecast API
              |
              v
        WeatherResponse

WeatherApi.Tests
      |
      v
WeatherApi project reference
```

Infrastructure and delivery flow:

```text
1 - build, lint, test, package
      |
      | uploads weather-api.zip
      v
2 - deploy-test
      |
      | re-uploads test-validated artifact
      v
3 - deploy-prod
      |
      v
Terraform target design

API Gateway HTTP API
      |
      v
AWS Lambda (.NET 10)
      |
      +-- CloudWatch logs
      +-- CloudWatch alarms
```

Runtime request flow:

```text
Client
  |
  | GET /weather?city=Sydney
  v
WeatherController
  |
  | validates query and logs request
  v
IWeatherProvider
  |
  +-- dev profile: MockWeatherProvider
  |
  +-- test/prod profile: OpenMeteoWeatherProvider
        |
        +-- Geocoding API: city -> latitude/longitude
        +-- Forecast API: latitude/longitude -> temperature/weather code
```

Key dependencies:

- `Scalar.AspNetCore`: local API reference UI.
- `Microsoft.AspNetCore.OpenApi`: OpenAPI generation.
- `Amazon.Lambda.AspNetCoreServer.Hosting`: adapts ASP.NET Core to API Gateway HTTP API events in Lambda.
- `xunit.v3`: unit tests.
- Terraform AWS provider: Lambda, API Gateway, IAM, CloudWatch logs, alarms, variables, and outputs.

## Run locally

Install the .NET 10 SDK first. This repo pins SDK `10.0.203` in `global.json`.

Download: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

```powershell
dotnet restore .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj
dotnet run --project .\src\WeatherApi\WeatherApi.csproj --launch-profile dev
```

Then try:

```text
GET http://localhost:5000/weather?city=Sydney
GET http://localhost:5000/health
```

The health endpoint returns:

```json
{
  "status": "ok"
}
```

The default local development profile uses mock data, so it does not need internet access.

## Scalar API UI

When running the `dev` profile, open Scalar at:

```text
http://localhost:5000/scalar/v1
```

When running the `test` profile, open Scalar at:

```text
http://localhost:5001/scalar/v1
```

The OpenAPI JSON is available at:

```text
http://localhost:5000/openapi/v1.json
http://localhost:5001/openapi/v1.json
```

Depending on your local profile, ASP.NET Core may choose a different port. Use the URL printed by `dotnet run`.

## Run profiles

This project has two local run profiles:

- `dev`: uses `ASPNETCORE_ENVIRONMENT=Development`, which loads `appsettings.Development.json` and uses `Mock`.
- `test`: uses `ASPNETCORE_ENVIRONMENT=Test`, which loads `appsettings.Test.json` and uses `OpenMeteo`.

Run the dev profile with mock data:

```powershell
dotnet run --project .\src\WeatherApi\WeatherApi.csproj --launch-profile dev
```

Run the test profile with live Open-Meteo data:

```powershell
dotnet run --project .\src\WeatherApi\WeatherApi.csproj --launch-profile test
```

The test profile listens on `http://localhost:5001` by default so it does not clash with the dev profile on `http://localhost:5000`.

## Configure weather provider

The app reads `Weather:Provider` from configuration. Valid values are:

- `OpenMeteo`: live weather from Open-Meteo Geocoding API plus Forecast API.
- `Mock`: deterministic in-memory weather data, useful when offline.

The production default is set in `src/WeatherApi/appsettings.json`:

```json
{
  "Weather": {
    "Provider": "OpenMeteo",
    "ApiUrl": {
      "GeocodingBaseUrl": "https://geocoding-api.open-meteo.com",
      "ForecastBaseUrl": "https://api.open-meteo.com"
    }
  }
}
```

Development is set in `src/WeatherApi/appsettings.Development.json`:

```json
{
  "Weather": {
    "Provider": "Mock"
  }
}
```

Test is set in `src/WeatherApi/appsettings.Test.json`:

```json
{
  "Weather": {
    "Provider": "OpenMeteo",
    "ApiUrl": {
      "GeocodingBaseUrl": "https://geocoding-api.open-meteo.com",
      "ForecastBaseUrl": "https://api.open-meteo.com"
    }
  }
}
```

In AWS Lambda, Terraform sets the same configuration as an environment variable:

```hcl
Weather__Provider = var.weather_provider
Weather__ApiUrl__GeocodingBaseUrl = var.open_meteo_geocoding_base_url
Weather__ApiUrl__ForecastBaseUrl = var.open_meteo_forecast_base_url
```

## How Open-Meteo works

Open-Meteo is called when the active configuration sets `Weather:Provider` to `OpenMeteo`, such as the `test` profile.

The request flow is:

1. The client calls this API:

```text
GET /weather?city=Sydney
```

2. The API calls the Open-Meteo Geocoding API to turn the city name into coordinates:

```text
https://geocoding-api.open-meteo.com/v1/search?name=Sydney&count=1&language=en&format=json
```

3. The API uses the returned latitude and longitude to call the Open-Meteo Forecast API:

```text
https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code
```

4. The Open-Meteo `weather_code` is converted into a readable condition such as `Clear sky`, `Rain`, or `Thunderstorm`.

5. This API returns a simple response:

```json
{
  "city": "Sydney, Australia",
  "tempC": 22,
  "condition": "Clear sky",
  "source": "open-meteo",
  "observedAt": "2026-05-12T10:30:00+00:00"
}
```

Open-Meteo does not need an API key for this use case. If the geocoding API cannot find the city, this API returns `404 City was not found`.

If Open-Meteo is unavailable, times out, or returns a network-level error, this API returns `503 Weather provider unavailable` with guidance to retry later or use the `Mock` provider for offline local testing.

Error responses use ASP.NET `ProblemDetails`:

```json
{
  "type": "about:blank",
  "title": "City is required",
  "status": 400,
  "detail": "Provide a non-empty city query string, for example /weather?city=Sydney."
}
```

## Run Lambda locally without Docker

For this project, Docker is not required. The app is built as a normal ASP.NET Core Web API and includes `AddAWSLambdaHosting(LambdaEventSource.HttpApi)` so the same application can run in two modes:

- **Local development:** `dotnet run` starts Kestrel and serves the API directly.
- **AWS Lambda:** the Lambda hosting package adapts API Gateway HTTP API events into ASP.NET Core requests.

That means local development is:

```powershell
dotnet run --project .\src\WeatherApi\WeatherApi.csproj --launch-profile dev
```

Then call:

```powershell
Invoke-RestMethod "http://localhost:5000/weather?city=Sydney"
```

For the technical test, this is enough to prove the API behavior locally without Docker or AWS credentials. The CI pipeline still creates a Lambda-ready zip package with `dotnet publish`.

## Test and lint

Stop any running `dotnet run` session before running tests. On Windows, the running API can lock the Debug executable and cause a file-copy error during test builds.

```powershell
dotnet format .\src\WeatherApi\WeatherApi.csproj --verify-no-changes
dotnet format .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj --verify-no-changes
dotnet test --project .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj
```

The CI pipeline runs tests in `Release` mode:

```powershell
dotnet test --project .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj --configuration Release
```

## Package

```powershell
dotnet publish .\src\WeatherApi\WeatherApi.csproj -c Release -r linux-x64 --self-contained false -o .\publish
Compress-Archive -Path .\publish\* -DestinationPath .\weather-api.zip -Force
```

The Terraform example expects the zip at `weather-api.zip` by default.

## CI/CD

The pipeline is split into three GitHub Actions workflows so the promotion path is explicit:

1. `1 - build, lint, test, package` (`.github/workflows/ci.yml`)

Runs on push to any branch, pull request, or manual trigger. It has two jobs:

- `build-test-package`: restore, lint, build, test, validate the response contract, publish, zip, and upload the Lambda package artifact.
- `iac-validate`: run Terraform formatting, initialization without a backend, and validation.

2. `2 - deploy-test` (`.github/workflows/deploy-test.yml`)

Runs after `1 - build, lint, test, package` succeeds on `main`, or manually with a source run ID. It downloads the Lambda artifact produced by CI, validates Terraform deployment inputs for the `test` environment, and re-uploads a test-validated artifact for prod deployment.

3. `3 - deploy-prod` (`.github/workflows/deploy-prod.yml`)

Runs after `2 - deploy-test` succeeds on `main`, or manually with a source run ID. It downloads the test-validated artifact and validates Terraform inputs for the `prod` environment.

The order is:

```text
1 - build, lint, test, package -> 2 - deploy-test -> 3 - deploy-prod
```

The test and prod workflows use GitHub Environments named `test` and `prod`. They intentionally stop before real deployment because this technical test requires no AWS credentials. With AWS configured, these jobs would use OIDC to assume an AWS role and run `terraform plan/apply`.

## Verify CI and Terraform locally

You can run the same core checks as GitHub Actions from PowerShell. This does not require Docker or AWS credentials.

Application checks:

```powershell
dotnet restore .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj
dotnet format .\src\WeatherApi\WeatherApi.csproj --verify-no-changes --verbosity normal
dotnet format .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj --verify-no-changes --verbosity normal
dotnet build .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj --configuration Release --no-restore
dotnet test --project .\tests\WeatherApi.Tests\WeatherApi.Tests.csproj --configuration Release --no-build
python .\scripts\validate_weather_response.py --file .\samples\weather-response.json
```

On Windows, use `py` instead of `python` if Python is installed through the Python launcher:

```powershell
py .\scripts\validate_weather_response.py --file .\samples\weather-response.json
```

If `terraform` is not found after installing it, run this in the current PowerShell window:

```powershell
$env:Path = "<terraform-folder>;$env:Path"
$env:TF_CLI_CONFIG_FILE = "<repo-root>\.terraformrc"
```

Package check:

```powershell
dotnet publish .\src\WeatherApi\WeatherApi.csproj --configuration Release --runtime linux-x64 --self-contained false --output .\publish
Compress-Archive -Path .\publish\* -DestinationPath .\weather-api.zip -Force
```

Terraform checks:

```powershell
cd .\infra\terraform
terraform fmt -check -recursive
terraform init -backend=false
terraform validate
cd ..\..
```

`terraform init -backend=false` downloads the Terraform provider plugin and validates the module without configuring remote state. `terraform validate` checks the IaC syntax and resource references, but it does not create AWS resources.

If Terraform is not installed, install it from:

```text
https://developer.hashicorp.com/terraform/install
```

## IaC

`infra/terraform` defines Lambda, API Gateway HTTP API, IAM execution role, CloudWatch logs, CloudWatch alarms, variables, and outputs. It is safe for review without credentials because the repository only validates the module.

Basic monitoring includes:

- Lambda application logs with retention.
- API Gateway access logs with retention.
- CloudWatch alarm for Lambda errors.
- CloudWatch alarm for API Gateway 5xx responses.

## Python helper

`scripts/validate_weather_response.py` validates the response contract expected from `GET /weather`.

Validate the included sample:

```powershell
python .\scripts\validate_weather_response.py --file .\samples\weather-response.json
```

Validate a running API response:

```powershell
python .\scripts\validate_weather_response.py --url "http://localhost:5000/weather?city=Sydney"
```

## AI usage

AI assistance was utilized to accelerate project scaffolding, initial documentation drafting, and the creation of CI/IaC templates. Additionally, AI was leveraged to quickly synthesize the Open-Meteo API documentation for faster integration of the weather endpoints. The design was checked against the provided test brief and current public documentation for .NET 10, Scalar, GitHub Actions, and AWS Lambda runtime support. The final solution was validated through local .NET SDK execution and manual verification of the GitHub Actions workflow.

## Assumptions, trade-offs, and improvements

Assumptions:

- No real AWS account or credentials are required for this submission.
- No Docker Desktop is required; the Lambda artifact is a zip package created with `dotnet publish`.
- The `dev` profile should be deterministic and offline-friendly, so it uses `Mock`.
- The `test` and production-style configuration use Open-Meteo for live weather data.

Trade-offs:

- The deploy workflows stop at deployment readiness because real AWS deployment is intentionally disabled.
- Error responses use ASP.NET `ProblemDetails`, which is standards-friendly but means success and error responses have different shapes.
- Open-Meteo calls are made directly without caching to keep the exercise focused and easy to review.

Improvements with more time:

- Add GitHub OIDC to assume an AWS deployment role and run real `terraform plan/apply`.
- Add protected GitHub Environment approvals for production.
- Add integration tests using `WebApplicationFactory`.
- Add caching for geocoding results and repeated weather lookups.
- Add structured logging enrichment and metrics with AWS Lambda Powertools for .NET.
- Add Terraform remote state and separate state per environment.
