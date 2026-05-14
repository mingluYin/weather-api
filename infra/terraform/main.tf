locals {
  # Use one consistent resource prefix so dev/prod environments can coexist.
  name        = "${var.app_name}-${var.environment}"
  package_sha = fileexists(var.lambda_package_path) ? filebase64sha256(var.lambda_package_path) : null

  tags = {
    Application = var.app_name
    Environment = var.environment
    ManagedBy   = "terraform"
  }
}

resource "aws_iam_role" "lambda_exec" {
  name = "${local.name}-lambda-exec"

  # Lambda needs permission to assume this role at runtime.
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })

  tags = local.tags
}

resource "aws_iam_role_policy_attachment" "lambda_basic_logging" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

// Retain logs for a fixed period to support troubleshooting without keeping
// development logs forever.
resource "aws_cloudwatch_log_group" "weather_api" {
  name              = "/aws/lambda/${local.name}"
  retention_in_days = var.log_retention_days

  tags = local.tags
}

resource "aws_lambda_function" "weather_api" {
  function_name = local.name
  role          = aws_iam_role.lambda_exec.arn
  # For .NET managed runtimes, the handler is the assembly name.
  handler          = "WeatherApi"
  runtime          = "dotnet10"
  architectures    = ["x86_64"]
  filename         = var.lambda_package_path
  source_code_hash = local.package_sha
  memory_size      = var.lambda_memory_mb
  timeout          = var.lambda_timeout_seconds

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT            = "Production"
      Weather__Provider                 = var.weather_provider
      Weather__ApiUrl__GeocodingBaseUrl = var.open_meteo_geocoding_base_url
      Weather__ApiUrl__ForecastBaseUrl  = var.open_meteo_forecast_base_url
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.weather_api,
    aws_iam_role_policy_attachment.lambda_basic_logging
  ]

  tags = local.tags
}

resource "aws_apigatewayv2_api" "weather_api" {
  name          = local.name
  protocol_type = "HTTP"

  tags = local.tags
}

resource "aws_apigatewayv2_integration" "lambda" {
  api_id = aws_apigatewayv2_api.weather_api.id
  # AWS_PROXY forwards the full HTTP API event to the Lambda-hosted ASP.NET app.
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.weather_api.invoke_arn
  payload_format_version = "2.0"
}

resource "aws_apigatewayv2_route" "weather" {
  api_id    = aws_apigatewayv2_api.weather_api.id
  route_key = "GET /weather"
  target    = "integrations/${aws_apigatewayv2_integration.lambda.id}"
}

resource "aws_apigatewayv2_route" "health" {
  api_id    = aws_apigatewayv2_api.weather_api.id
  route_key = "GET /health"
  target    = "integrations/${aws_apigatewayv2_integration.lambda.id}"
}

resource "aws_apigatewayv2_stage" "default" {
  api_id = aws_apigatewayv2_api.weather_api.id
  name   = "$default"
  # Auto-deploy keeps this example concise; production may use explicit stages
  # and promotion gates.
  auto_deploy = true

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.api_access.arn
    format = jsonencode({
      requestId      = "$context.requestId"
      ip             = "$context.identity.sourceIp"
      requestTime    = "$context.requestTime"
      httpMethod     = "$context.httpMethod"
      routeKey       = "$context.routeKey"
      status         = "$context.status"
      protocol       = "$context.protocol"
      responseLength = "$context.responseLength"
    })
  }

  tags = local.tags
}

resource "aws_cloudwatch_log_group" "api_access" {
  name              = "/aws/apigateway/${local.name}"
  retention_in_days = var.log_retention_days

  tags = local.tags
}

resource "aws_lambda_permission" "allow_http_api" {
  statement_id  = "AllowExecutionFromHttpApi"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.weather_api.function_name
  principal     = "apigateway.amazonaws.com"
  # Restrict invocation permission to this API Gateway instance.
  source_arn = "${aws_apigatewayv2_api.weather_api.execution_arn}/*/*"
}

resource "aws_cloudwatch_metric_alarm" "lambda_errors" {
  alarm_name          = "${local.name}-lambda-errors"
  alarm_description   = "Lambda reported errors for the weather API."
  namespace           = "AWS/Lambda"
  metric_name         = "Errors"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = var.lambda_error_alarm_threshold
  comparison_operator = "GreaterThanOrEqualToThreshold"
  treat_missing_data  = "notBreaching"

  dimensions = {
    FunctionName = aws_lambda_function.weather_api.function_name
  }

  tags = local.tags
}

resource "aws_cloudwatch_metric_alarm" "api_5xx" {
  alarm_name          = "${local.name}-api-5xx"
  alarm_description   = "API Gateway returned 5xx responses for the weather API."
  namespace           = "AWS/ApiGateway"
  metric_name         = "5xx"
  statistic           = "Sum"
  period              = 300
  evaluation_periods  = 1
  threshold           = var.api_5xx_alarm_threshold
  comparison_operator = "GreaterThanOrEqualToThreshold"
  treat_missing_data  = "notBreaching"

  dimensions = {
    ApiId = aws_apigatewayv2_api.weather_api.id
    Stage = aws_apigatewayv2_stage.default.name
  }

  tags = local.tags
}
