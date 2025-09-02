using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using System.Collections.Concurrent;

namespace Survey.Infrastructure.Services;

public class InMemoryQueueService : IQueueService
{
    private readonly ConcurrentQueue<ProcessingQueueItem> _queue = new();
    private readonly ConcurrentDictionary<string, ProcessingQueueItem> _processingItems = new();
    private readonly ILogger<InMemoryQueueService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public InMemoryQueueService(ILogger<InMemoryQueueService> logger)
    {
        _logger = logger;
    }

    public async Task<string> EnqueueForProcessingAsync(Survey.Core.Models.SurveyResponse surveyResponse)
    {
        try
        {
            var processingId = Guid.NewGuid().ToString();
            var queueItem = new ProcessingQueueItem
            {
                ProcessingId = processingId,
                ResponseId = surveyResponse.ResponseId,
                ClientId = surveyResponse.ClientId,
                SurveyId = surveyResponse.SurveyId,
                Status = "pending",
                RetryCount = 0,
                MaxRetries = 3,
                ScheduledAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _queue.Enqueue(queueItem);
            _processingItems[processingId] = queueItem;

            _logger.LogInformation("Enqueued survey response for processing: {ProcessingId}", processingId);
            return processingId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing survey response for processing: {ResponseId}", surveyResponse.ResponseId);
            throw;
        }
    }

    public async Task<ProcessingQueueItem?> DequeueForProcessingAsync()
    {
        try
        {
            await _semaphore.WaitAsync();

            if (_queue.TryDequeue(out var item))
            {
                item.Status = "processing";
                _logger.LogInformation("Dequeued item for processing: {ProcessingId}", item.ProcessingId);
                return item;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dequeuing item for processing");
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> MarkAsProcessedAsync(string processingId)
    {
        try
        {
            if (_processingItems.TryGetValue(processingId, out var item))
            {
                item.Status = "completed";
                item.ProcessedAt = DateTime.UtcNow;
                _logger.LogInformation("Marked item as processed: {ProcessingId}", processingId);
                return true;
            }

            _logger.LogWarning("Processing item not found: {ProcessingId}", processingId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking item as processed: {ProcessingId}", processingId);
            throw;
        }
    }

    public async Task<bool> MarkAsFailedAsync(string processingId, string errorMessage)
    {
        try
        {
            if (_processingItems.TryGetValue(processingId, out var item))
            {
                item.Status = "failed";
                item.ErrorMessage = errorMessage;
                item.ProcessedAt = DateTime.UtcNow;
                _logger.LogWarning("Marked item as failed: {ProcessingId}, Error: {ErrorMessage}", processingId, errorMessage);
                return true;
            }

            _logger.LogWarning("Processing item not found: {ProcessingId}", processingId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking item as failed: {ProcessingId}", processingId);
            throw;
        }
    }

    public async Task<int> GetQueueLengthAsync()
    {
        return _queue.Count;
    }

    public async Task<ProcessingQueueItem?> GetProcessingItemAsync(string processingId)
    {
        _processingItems.TryGetValue(processingId, out var item);
        return item;
    }

    public async Task<IEnumerable<ProcessingQueueItem>> GetFailedItemsAsync(int maxRetries = 3)
    {
        return _processingItems.Values
            .Where(item => item.Status == "failed" && item.RetryCount < maxRetries)
            .ToList();
    }
}
