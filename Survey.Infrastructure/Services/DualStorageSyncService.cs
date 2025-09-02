using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using Survey.Infrastructure.Data;
using System.Text.Json;

namespace Survey.Infrastructure.Services;

public class DualStorageSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DualStorageSyncService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(5);
    private readonly int _batchSize = 100;

    public DualStorageSyncService(
        IServiceProvider serviceProvider,
        ILogger<DualStorageSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dual storage sync service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSyncQueueAsync();
                await Task.Delay(_processingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Dual storage sync service stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in dual storage sync service");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Dual storage sync service stopped");
    }

    private async Task ProcessSyncQueueAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SurveyResponseDbContext>();
        var fastStorage = scope.ServiceProvider.GetRequiredService<IFastStorageService>();

        try
        {
            // Get pending sync items
            var pendingItems = await GetPendingSyncItemsAsync(context);
            
            if (!pendingItems.Any())
            {
                return; // No work to do
            }

            _logger.LogDebug("Processing {Count} sync items", pendingItems.Count);

            foreach (var item in pendingItems)
            {
                try
                {
                    await ProcessSyncItemAsync(item, fastStorage, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing sync item {SyncId} for response {ResponseId}", 
                        item.SyncId, item.ResponseId);
                    
                    await MarkSyncItemAsFailedAsync(context, item, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in sync queue processing");
        }
    }

    private async Task<List<DualStorageSyncQueueEntity>> GetPendingSyncItemsAsync(SurveyResponseDbContext context)
    {
        // This would query the DualStorageSyncQueue table
        // For now, simulate with an empty list since the table doesn't exist yet
        return new List<DualStorageSyncQueueEntity>();
    }

    private async Task ProcessSyncItemAsync(
        DualStorageSyncQueueEntity syncItem, 
        IFastStorageService fastStorage, 
        SurveyResponseDbContext context)
    {
        _logger.LogDebug("Processing sync item {SyncId} for response {ResponseId}", 
            syncItem.SyncId, syncItem.ResponseId);

        // Mark as processing
        syncItem.Status = "processing";
        await context.SaveChangesAsync();

        // Deserialize the data
        var surveyResponse = JsonSerializer.Deserialize<Survey.Infrastructure.Data.Entities.SurveyResponseEntity>(syncItem.FastStorageData!);
        if (surveyResponse == null)
        {
            throw new InvalidOperationException($"Failed to deserialize sync data for {syncItem.ResponseId}");
        }

        // Convert to fast storage format
        var fastStorageResponse = FastStorageSurveyResponse.FromSurveyResponse(new SurveyResponse
        {
            SurveyId = surveyResponse.SurveyId,
            ClientId = surveyResponse.ClientId,
            ResponseId = surveyResponse.ResponseId,
            Responses = new SurveyResponses
            {
                NpsScore = surveyResponse.NpsScore,
                Satisfaction = surveyResponse.Satisfaction,
                CustomFields = JsonSerializer.Deserialize<Dictionary<string, object>>(surveyResponse.CustomFields ?? "{}") ?? new()
            },
            Metadata = new ResponseMetadata
            {
                UserAgent = surveyResponse.UserAgent,
                IpAddress = surveyResponse.IpAddress,
                SessionId = surveyResponse.SessionId,
                DeviceType = surveyResponse.DeviceType
            },
            ProcessingStatus = surveyResponse.ProcessingStatus,
            CreatedAt = surveyResponse.CreatedAt,
            UpdatedAt = surveyResponse.UpdatedAt
        });

        // Perform the sync operation
        bool syncSuccess = false;
        switch (syncItem.Operation)
        {
            case "CREATE":
            case "UPDATE":
                syncSuccess = await fastStorage.SetAsync(
                    surveyResponse.ResponseId,
                    surveyResponse.ClientId,
                    fastStorageResponse,
                    TimeSpan.FromHours(24)
                );
                break;

            case "DELETE":
                syncSuccess = await fastStorage.DeleteAsync(surveyResponse.ResponseId, surveyResponse.ClientId);
                break;

            default:
                throw new InvalidOperationException($"Unknown sync operation: {syncItem.Operation}");
        }

        if (syncSuccess)
        {
            // Mark as completed
            syncItem.Status = "completed";
            syncItem.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            _logger.LogDebug("Sync item {SyncId} completed successfully", syncItem.SyncId);
        }
        else
        {
            throw new InvalidOperationException($"Sync operation failed for {syncItem.ResponseId}");
        }
    }

    private async Task MarkSyncItemAsFailedAsync(
        SurveyResponseDbContext context, 
        DualStorageSyncQueueEntity syncItem, 
        string errorMessage)
    {
        syncItem.Status = "failed";
        syncItem.ErrorMessage = errorMessage;
        syncItem.RetryCount++;
        syncItem.LastSyncAttempt = DateTime.UtcNow;

        if (syncItem.RetryCount >= syncItem.MaxRetries)
        {
            _logger.LogWarning("Sync item {SyncId} exceeded max retries ({MaxRetries})", 
                syncItem.SyncId, syncItem.MaxRetries);
        }

        await context.SaveChangesAsync();
    }

    public override void Dispose()
    {
        _logger.LogInformation("Dual storage sync service disposed");
        base.Dispose();
    }
}

/// <summary>
/// Entity for dual storage sync queue (placeholder for future database table)
/// </summary>
public class DualStorageSyncQueueEntity
{
    public string SyncId { get; set; } = string.Empty;
    public string ResponseId { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // CREATE, UPDATE, DELETE
    public string? FastStorageData { get; set; } // JSON serialized
    public string Status { get; set; } = "pending"; // pending, processing, completed, failed
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? LastSyncAttempt { get; set; }
}
