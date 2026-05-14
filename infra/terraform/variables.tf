variable "aws_region" {
  description = "AWS region used for the example deployment."
  type        = string
  default     = "ap-southeast-2"
}

variable "app_name" {
  description = "Application name used for AWS resource names."
  type        = string
  default     = "weather-api"
}

variable "environment" {
  description = "Deployment environment name."
  type        = string
  default     = "dev"
}

variable "lambda_package_path" {
  description = "Path to the zipped dotnet publish output."
  type        = string
  default     = "../../weather-api.zip"
}

variable "lambda_memory_mb" {
  description = "Lambda memory allocation."
  type        = number
  default     = 256
}

variable "lambda_timeout_seconds" {
  description = "Lambda timeout in seconds."
  type        = number
  default     = 10
}

variable "log_retention_days" {
  description = "CloudWatch log retention for the Lambda function."
  type        = number
  default     = 14
}

variable "weather_provider" {
  description = "Weather provider implementation. Use OpenMeteo for live data or Mock for deterministic offline responses."
  type        = string
  default     = "OpenMeteo"

  validation {
    condition     = contains(["OpenMeteo", "Mock"], var.weather_provider)
    error_message = "weather_provider must be OpenMeteo or Mock."
  }
}

variable "open_meteo_geocoding_base_url" {
  description = "Open-Meteo Geocoding API base URL."
  type        = string
  default     = "https://geocoding-api.open-meteo.com"
}

variable "open_meteo_forecast_base_url" {
  description = "Open-Meteo Forecast API base URL."
  type        = string
  default     = "https://api.open-meteo.com"
}

variable "lambda_error_alarm_threshold" {
  description = "Number of Lambda errors in a 5-minute period before the alarm enters ALARM state."
  type        = number
  default     = 1
}

variable "api_5xx_alarm_threshold" {
  description = "Number of API Gateway 5xx responses in a 5-minute period before the alarm enters ALARM state."
  type        = number
  default     = 1
}
