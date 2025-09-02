using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using System.ComponentModel.DataAnnotations;
using SurveyApi.Models;

namespace SurveyApi.Controllers;

[ApiController]
[Route("api/v1")]
public class SurveyResponseController : ControllerBase
{
    private readonly ISurveyResponseService _surveyResponseService;
    private readonly IValidationService _validationService;
    private readonly IStorageOrchestrator _storageOrchestrator;
    private readonly ILogger<SurveyResponseController> _logger;

    public SurveyResponseController(
        ISurveyResponseService surveyResponseService,
        IValidationService validationService,
        IStorageOrchestrator storageOrchestrator,
        ILogger<SurveyResponseController> logger)
    {
        _surveyResponseService = surveyResponseService;
        _validationService = validationService;
        _storageOrchestrator = storageOrchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Submit a new survey response for processing
    /// </summary>
    [HttpPost("survey-responses")]
    public async Task<IActionResult> SubmitSurveyResponse([FromBody] SurveyResponseRequest request)
    {
        try
        {
            _logger.LogInformation("Received survey response request: {ResponseId}", request.ResponseId);

            // Validate the request
            var validationResult = await _validationService.ValidateSurveyResponseAsync(request);
            if (!validationResult.IsValid)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = "VALIDATION_ERROR",
                        Message = "Invalid survey response data",
                        Details = validationResult.Errors.Select(e => new ValidationDetail
                        {
                            Field = e.Field,
                            Message = e.Message
                        }).ToList()
                    },
                    Timestamp = DateTime.UtcNow,
                    RequestId = HttpContext.TraceIdentifier
                });
            }

            // Process the survey response
            var result = await _surveyResponseService.ProcessSurveyResponseAsync(request);

            _logger.LogInformation("Successfully processed survey response: {ResponseId}", request.ResponseId);

            return CreatedAtAction(nameof(GetProcessingStatus), new { processingId = result.ProcessingId }, 
                new ApiResponse<SurveyResponseResult>
                {
                    Success = true,
                    Data = result,
                    Message = "Survey response accepted for processing",
                    Timestamp = DateTime.UtcNow,
                    RequestId = HttpContext.TraceIdentifier
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing survey response: {ResponseId}", request.ResponseId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while processing the request"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get real-time aggregated NPS scores for a specific client
    /// </summary>
    [HttpGet("metrics/nps/{clientId}")]
    public async Task<IActionResult> GetNpsMetrics(
        [Required] string clientId,
        [FromQuery] string? surveyId = null,
        [FromQuery] string period = "day",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            _logger.LogInformation("Getting NPS metrics for client: {ClientId}", clientId);

            var metrics = await _surveyResponseService.GetNpsMetricsAsync(clientId, surveyId, period);

            return Ok(new ApiResponse<NpsMetrics>
            {
                Success = true,
                Data = metrics,
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NPS metrics for client: {ClientId}", clientId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while retrieving metrics"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Check the processing status of a survey response
    /// </summary>
    [HttpGet("processing/{processingId}")]
    public async Task<IActionResult> GetProcessingStatus([Required] string processingId)
    {
        try
        {
            _logger.LogInformation("Getting processing status: {ProcessingId}", processingId);

            var status = await _surveyResponseService.GetProcessingStatusAsync(processingId);

            return Ok(new ApiResponse<ProcessingStatus>
            {
                Success = true,
                Data = status,
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processing status: {ProcessingId}", processingId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while retrieving processing status"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get survey responses for a specific client
    /// </summary>
    [HttpGet("clients/{clientId}/responses")]
    public async Task<IActionResult> GetClientResponses(
        [Required] string clientId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        try
        {
            _logger.LogInformation("Getting responses for client: {ClientId}", clientId);

            var responses = await _surveyResponseService.GetResponsesByClientAsync(clientId, skip, take);

            return Ok(new ApiResponse<IEnumerable<SurveyResponse>>
            {
                Success = true,
                Data = responses,
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting responses for client: {ClientId}", clientId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while retrieving responses"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Get survey responses for a specific client with detailed dual storage metadata
    /// </summary>
    [HttpGet("clients/{clientId}/responses/metadata")]
    public async Task<IActionResult> GetClientResponsesWithMetadata(
        [Required] string clientId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        try
        {
            _logger.LogInformation("Getting responses with metadata for client: {ClientId}", clientId);

            var result = await _storageOrchestrator.GetResponsesByClientWithMetadataAsync(clientId, skip, take);

            return Ok(new ApiResponse<DualStorageQueryResult<SurveyResponse>>
            {
                Success = true,
                Data = result,
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting responses with metadata for client: {ClientId}", clientId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while retrieving responses with metadata"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> HealthCheck()
    {
        try
        {
            var storageHealth = await _storageOrchestrator.GetStorageHealthAsync();
            
            return Ok(new ApiResponse<HealthStatus>
            {
                Success = true,
                Data = new HealthStatus
                {
                    Status = "healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Components = new Dictionary<string, ComponentStatus>
                    {
                        ["database"] = new ComponentStatus { Status = "healthy", ResponseTime = "15ms" },
                        ["cache"] = new ComponentStatus { Status = "healthy", ResponseTime = "2ms" },
                        ["queue"] = new ComponentStatus { Status = "healthy", PendingItems = 5 }
                    },
                    Metrics = new HealthMetrics
                    {
                        RequestsPerSecond = 150,
                        AverageResponseTime = "45ms",
                        ErrorRate = 0.01m
                    },
                    Storage = storageHealth
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while retrieving health status"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    // New dual storage specific endpoints

    [HttpGet("storage/consistency/{responseId}")]
    public async Task<IActionResult> CheckConsistency(
        [Required] string responseId,
        [Required] string clientId)
    {
        try
        {
            var isConsistent = await _storageOrchestrator.CheckConsistencyAsync(responseId, clientId);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new
                {
                    responseId,
                    clientId,
                    isConsistent,
                    checkedAt = DateTime.UtcNow
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking consistency for response: {ResponseId}", responseId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while checking consistency"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpPost("storage/sync/{responseId}")]
    public async Task<IActionResult> ForceSync(
        [Required] string responseId,
        [Required] string clientId)
    {
        try
        {
            var success = await _storageOrchestrator.ForceSyncAsync(responseId, clientId);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Data = new
                {
                    responseId,
                    clientId,
                    syncSuccess = success,
                    syncedAt = DateTime.UtcNow
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forcing sync for response: {ResponseId}", responseId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while forcing sync"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("storage/health")]
    public async Task<IActionResult> GetStorageHealth()
    {
        try
        {
            var health = await _storageOrchestrator.GetStorageHealthAsync();
            return Ok(new ApiResponse<StorageHealthMetrics>
            {
                Success = true,
                Data = health,
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting storage health");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while retrieving storage health"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }

    [HttpGet("storage/responses/{responseId}")]
    public async Task<IActionResult> GetResponseFromStorage(
        [Required] string responseId,
        [Required] string clientId)
    {
        try
        {
            var response = await _storageOrchestrator.GetSurveyResponseAsync(responseId, clientId);
            
            if (response == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = new ApiError
                    {
                        Code = "NOT_FOUND",
                        Message = "Response not found"
                    },
                    Timestamp = DateTime.UtcNow,
                    RequestId = HttpContext.TraceIdentifier
                });
            }
            
            return Ok(new ApiResponse<SurveyResponse>
            {
                Success = true,
                Data = response,
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting response from storage: {ResponseId}", responseId);
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "An internal error occurred while retrieving response from storage"
                },
                Timestamp = DateTime.UtcNow,
                RequestId = HttpContext.TraceIdentifier
            });
        }
    }
}
