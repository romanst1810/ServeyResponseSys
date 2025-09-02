using Survey.Core.Models;

namespace Survey.Core.Interfaces;

public interface ISurveyResponseService
{
    Task<SurveyResponseResult> ProcessSurveyResponseAsync(SurveyResponseRequest request);
    Task<NpsMetrics> GetNpsMetricsAsync(string clientId, string? surveyId = null, string period = "day");
    Task<ProcessingStatus> GetProcessingStatusAsync(string processingId);
    Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetResponsesByClientAsync(string clientId, int skip = 0, int take = 100);
    Task<bool> ValidateSurveyResponseAsync(SurveyResponseRequest request);
}

public interface IQueueService
{
    Task<string> EnqueueForProcessingAsync(Survey.Core.Models.SurveyResponse surveyResponse);
    Task<ProcessingQueueItem?> DequeueForProcessingAsync();
    Task<bool> MarkAsProcessedAsync(string processingId);
    Task<bool> MarkAsFailedAsync(string processingId, string errorMessage);
    Task<int> GetQueueLengthAsync();
}

public interface IMetricsService
{
    Task<NpsMetrics> CalculateNpsMetricsAsync(string clientId, string? surveyId = null, string period = "day");
    Task UpdateMetricsAsync(string clientId, Survey.Core.Models.SurveyResponse surveyResponse);
    Task<NpsMetrics> GetCachedMetricsAsync(string clientId, string? surveyId = null);
    Task CacheMetricsAsync(string clientId, NpsMetrics metrics);
}

public interface IValidationService
{
    Task<ValidationResult> ValidateSurveyResponseAsync(SurveyResponseRequest request);
    Task<bool> ValidateClientAccessAsync(string clientId, string apiKey);
    Task<bool> ValidateRateLimitAsync(string clientId);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
}

public class ValidationError
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ProcessingStatus
{
    public string ProcessingId { get; set; } = string.Empty;
    public string ResponseId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public List<ProcessingStage> Stages { get; set; } = new();
    public ProcessingResult? Result { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ProcessingStage
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
}

public class ProcessingResult
{
    public bool StoredInFastStorage { get; set; }
    public bool StoredInRelationalDb { get; set; }
    public bool MetricsUpdated { get; set; }
}
