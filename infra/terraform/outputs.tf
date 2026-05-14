output "api_endpoint" {
  description = "Base URL for the HTTP API."
  value       = aws_apigatewayv2_api.weather_api.api_endpoint
}

output "lambda_function_name" {
  description = "Lambda function name."
  value       = aws_lambda_function.weather_api.function_name
}

output "cloudwatch_log_group" {
  description = "CloudWatch log group for application logs."
  value       = aws_cloudwatch_log_group.weather_api.name
}

output "lambda_error_alarm_name" {
  description = "CloudWatch alarm name for Lambda errors."
  value       = aws_cloudwatch_metric_alarm.lambda_errors.alarm_name
}

output "api_5xx_alarm_name" {
  description = "CloudWatch alarm name for API Gateway 5xx responses."
  value       = aws_cloudwatch_metric_alarm.api_5xx.alarm_name
}
