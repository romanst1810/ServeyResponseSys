using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;

namespace Survey.Infrastructure.Services;

public class SurveyResponseService : ISurveyResponseService
{
    private readonly IStorageOrchestrator _storageOrchestrator;
    private readonly IMetricsService _metricsService;
    private readonly IValidationService _validationService;
    private readonly ILogger<SurveyResponseService> _logger;

    public SurveyResponseService(
        IStorageOrchestrator storageOrchestrator,
        IMetricsService metricsService,
        IValidationService validationService,
        ILogger<SurveyResponseService> logger)
    {
        _storageOrchestrator = storageOrchestrator;
        _metricsService = metricsService;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task<SurveyResponseResult> ProcessSurveyResponseAsync(SurveyResponseRequest request)
    {
        try
        {
            _logger.LogInformation("Processing survey response: {ResponseId}", request.ResponseId);

            var validationResult = await _validationService.ValidateSurveyResponseAsync(request);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Survey response validation failed: {string.Join(", ", validationResult.Errors.Select(e => e.Message))}");
            }

            // Use storage orchestrator for dual storage processing
            var result = await _storageOrchestrator.ProcessSurveyResponseAsync(request);

            _logger.LogInformation("Survey response processed successfully: {ResponseId}", request.ResponseId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing survey response: {ResponseId}", request.ResponseId);
            throw;
        }
    }

    public async Task<NpsMetrics> GetNpsMetricsAsync(string clientId, string? surveyId = null, string period = "day")
    {
        try
        {
            _logger.LogInformation("Getting NPS metrics for client: {ClientId}, Survey: {SurveyId}, Period: {Period}", 
                clientId, surveyId, period);

            var metrics = await _metricsService.CalculateNpsMetricsAsync(clientId, surveyId, period);

            _logger.LogInformation("NPS metrics retrieved successfully for client: {ClientId}", clientId);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NPS metrics for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<ProcessingStatus> GetProcessingStatusAsync(string processingId)
    {
        try
        {
            _logger.LogInformation("Getting processing status: {ProcessingId}", processingId);

            return new ProcessingStatus
            {
                ProcessingId = processingId,
                ResponseId = "response_123", 
                Status = "completed",
                Progress = 100,
                Stages = new List<ProcessingStage>
                {
                    new() { Name = "validation", Status = "completed", CompletedAt = DateTime.UtcNow.AddSeconds(-3) },
                    new() { Name = "storage", Status = "completed", CompletedAt = DateTime.UtcNow.AddSeconds(-2) },
                    new() { Name = "analytics", Status = "completed", CompletedAt = DateTime.UtcNow.AddSeconds(-1) }
                },
                Result = new ProcessingResult
                {
                    StoredInFastStorage = true,
                    StoredInRelationalDb = true,
                    MetricsUpdated = true
                },
                CreatedAt = DateTime.UtcNow.AddSeconds(-5),
                CompletedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting processing status: {ProcessingId}", processingId);
            throw;
        }
    }

    public async Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetResponsesByClientAsync(string clientId, int skip = 0, int take = 100)
    {
        try
        {
            _logger.LogInformation("Getting responses for client: {ClientId}", clientId);

            var responses = await _storageOrchestrator.GetResponsesByClientAsync(clientId, skip, take);

            _logger.LogInformation("Retrieved {Count} responses for client: {ClientId}", responses.Count(), clientId);
            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting responses for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<bool> ValidateSurveyResponseAsync(SurveyResponseRequest request)
    {
        try
        {
            _logger.LogInformation("Validating survey response: {ResponseId}", request.ResponseId);

            var validationResult = await _validationService.ValidateSurveyResponseAsync(request);

            _logger.LogInformation("Survey response validation result: {IsValid} for {ResponseId}", validationResult.IsValid, request.ResponseId);
            return validationResult.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating survey response: {ResponseId}", request.ResponseId);
            throw;
        }
    }
}
