using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;

namespace Survey.Infrastructure.Services;

public class ValidationService : IValidationService
{
    private readonly ILogger<ValidationService> _logger;

    public ValidationService(ILogger<ValidationService> logger)
    {
        _logger = logger;
    }

    public async Task<Core.Interfaces.ValidationResult> ValidateSurveyResponseAsync(SurveyResponseRequest request)
    {
        var result = new Core.Interfaces.ValidationResult { IsValid = true };

        try
        {
            // Validate required fields
            if (string.IsNullOrEmpty(request.ResponseId))
            {
                result.Errors.Add(new ValidationError { Field = "responseId", Message = "Response ID is required" });
                result.IsValid = false;
            }

            if (string.IsNullOrEmpty(request.SurveyId))
            {
                result.Errors.Add(new ValidationError { Field = "surveyId", Message = "Survey ID is required" });
                result.IsValid = false;
            }

            if (string.IsNullOrEmpty(request.ClientId))
            {
                result.Errors.Add(new ValidationError { Field = "clientId", Message = "Client ID is required" });
                result.IsValid = false;
            }

            // Validate NPS score
            if (request.Responses.NpsScore.HasValue)
            {
                if (request.Responses.NpsScore < 0 || request.Responses.NpsScore > 10)
                {
                    result.Errors.Add(new ValidationError 
                    { 
                        Field = "responses.nps_score", 
                        Message = "NPS score must be between 0 and 10" 
                    });
                    result.IsValid = false;
                }
            }

            // Validate satisfaction value
            if (!string.IsNullOrEmpty(request.Responses.Satisfaction))
            {
                var validSatisfactionValues = new[] 
                { 
                    "very_satisfied", "satisfied", "neutral", "dissatisfied", "very_dissatisfied" 
                };
                
                if (!validSatisfactionValues.Contains(request.Responses.Satisfaction))
                {
                    result.Errors.Add(new ValidationError 
                    { 
                        Field = "responses.satisfaction", 
                        Message = "Satisfaction value must be one of: very_satisfied, satisfied, neutral, dissatisfied, very_dissatisfied" 
                    });
                    result.IsValid = false;
                }
            }

            // Validate custom fields (basic validation)
            if (request.Responses.CustomFields != null)
            {
                foreach (var field in request.Responses.CustomFields)
                {
                    if (field.Value is string stringValue && stringValue.Length > 1000)
                    {
                        result.Errors.Add(new ValidationError 
                        { 
                            Field = $"responses.custom_fields.{field.Key}", 
                            Message = "Custom field value cannot exceed 1000 characters" 
                        });
                        result.IsValid = false;
                    }
                }
            }

            // Validate metadata
            if (request.Metadata != null)
            {
                if (!string.IsNullOrEmpty(request.Metadata.IpAddress))
                {
                    if (!IsValidIpAddress(request.Metadata.IpAddress))
                    {
                        result.Errors.Add(new ValidationError 
                        { 
                            Field = "metadata.ip_address", 
                            Message = "Invalid IP address format" 
                        });
                        result.IsValid = false;
                    }
                }

                if (!string.IsNullOrEmpty(request.Metadata.DeviceType))
                {
                    var validDeviceTypes = new[] { "mobile", "desktop", "tablet" };
                    if (!validDeviceTypes.Contains(request.Metadata.DeviceType.ToLower()))
                    {
                        result.Errors.Add(new ValidationError 
                        { 
                            Field = "metadata.device_type", 
                            Message = "Device type must be one of: mobile, desktop, tablet" 
                        });
                        result.IsValid = false;
                    }
                }
            }

            if (!result.IsValid)
            {
                _logger.LogWarning("Survey response validation failed: {ResponseId}, Errors: {ErrorCount}", 
                    request.ResponseId, result.Errors.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating survey response: {ResponseId}", request.ResponseId);
            result.IsValid = false;
            result.Errors.Add(new ValidationError 
            { 
                Field = "general", 
                Message = "An error occurred during validation" 
            });
            return result;
        }
    }

    public async Task<bool> ValidateClientAccessAsync(string clientId, string apiKey)
    {
        try
        {
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(apiKey))
                return false;

            // Simulate API key validation (in real implementation, this would check against database)
            if (apiKey.Length < 10)
                return false;

            // Simulate client existence check
            if (!clientId.StartsWith("client_"))
                return false;

            _logger.LogInformation("Client access validated successfully: {ClientId}", clientId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client access: {ClientId}", clientId);
            return false;
        }
    }

    public async Task<bool> ValidateRateLimitAsync(string clientId)
    {
        try
        {
            // Simulate rate limit check (in real implementation, this would use Redis or similar)
            var random = new Random();
            var isRateLimited = random.Next(100) < 5; // 5% chance of rate limiting for demo

            if (isRateLimited)
            {
                _logger.LogWarning("Rate limit exceeded for client: {ClientId}", clientId);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rate limit for client: {ClientId}", clientId);
            return false;
        }
    }

    private static bool IsValidIpAddress(string ipAddress)
    {
        try
        {
            var parts = ipAddress.Split('.');
            if (parts.Length != 4)
                return false;

            foreach (var part in parts)
            {
                if (!int.TryParse(part, out var num) || num < 0 || num > 255)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
