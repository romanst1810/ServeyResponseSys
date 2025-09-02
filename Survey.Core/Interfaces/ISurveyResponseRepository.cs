using Survey.Core.Models;

namespace Survey.Core.Interfaces;

public interface ISurveyResponseRepository
{
    Task<Survey.Core.Models.SurveyResponse?> GetByIdAsync(string responseId);
    Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetByClientIdAsync(string clientId, int skip = 0, int take = 100);
    Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetBySurveyIdAsync(string surveyId, int skip = 0, int take = 100);
    Task<Survey.Core.Models.SurveyResponse> CreateAsync(Survey.Core.Models.SurveyResponse surveyResponse);
    Task<Survey.Core.Models.SurveyResponse> UpdateAsync(Survey.Core.Models.SurveyResponse surveyResponse);
    Task<bool> DeleteAsync(string responseId);
    Task<int> GetCountByClientIdAsync(string clientId);
    Task<int> GetCountBySurveyIdAsync(string surveyId);
    Task<decimal> GetAverageNpsByClientIdAsync(string clientId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<decimal> GetAverageNpsBySurveyIdAsync(string surveyId, DateTime? fromDate = null, DateTime? toDate = null);
}

public interface IFastStorageRepository
{
    Task<Survey.Core.Models.SurveyResponse?> GetByIdAsync(string responseId);
    Task<Survey.Core.Models.SurveyResponse> CreateAsync(Survey.Core.Models.SurveyResponse surveyResponse);
    Task<Survey.Core.Models.SurveyResponse> UpdateAsync(Survey.Core.Models.SurveyResponse surveyResponse);
    Task<bool> DeleteAsync(string responseId);
    Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetByClientIdAsync(string clientId, int skip = 0, int take = 100);
    Task<NpsMetrics?> GetNpsMetricsAsync(string clientId, string? surveyId = null, string period = "day");
    Task UpdateNpsMetricsAsync(string clientId, NpsMetrics metrics);
}

public interface IProcessingQueueRepository
{
    Task<ProcessingQueueItem> EnqueueAsync(ProcessingQueueItem item);
    Task<ProcessingQueueItem?> DequeueAsync();
    Task<ProcessingQueueItem> UpdateStatusAsync(string processingId, string status, string? errorMessage = null);
    Task<IEnumerable<ProcessingQueueItem>> GetFailedItemsAsync(int maxRetries = 3);
    Task<int> GetQueueLengthAsync();
}

public class ProcessingQueueItem
{
    public string ProcessingId { get; set; } = string.Empty;
    public string ResponseId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string SurveyId { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
