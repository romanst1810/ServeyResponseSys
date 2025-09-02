using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Survey.Infrastructure.Services;

/// <summary>
/// DynamoDB simulation service with advanced features
/// </summary>
public class DynamoDbSimulationService : IFastStorageService
{
    private readonly ConcurrentDictionary<string, FastStorageItem<object>> _storage = new();
    private readonly ConcurrentDictionary<string, FastStorageItem<object>> _gsiSurveyId = new();
    private readonly ConcurrentDictionary<string, FastStorageItem<object>> _gsiProcessingStatus = new();
    private readonly ILogger<DynamoDbSimulationService> _logger;
    private readonly Timer _cleanupTimer;
    private readonly object _metricsLock = new();
    private FastStorageMetrics _metrics = new();

    public DynamoDbSimulationService(ILogger<DynamoDbSimulationService> logger)
    {
        _logger = logger;
        
        // Start cleanup timer to remove expired items
        _cleanupTimer = new Timer(CleanupExpiredItems, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        
        _logger.LogInformation("DynamoDB simulation service initialized");
    }

    public async Task<T?> GetAsync<T>(string key, string partitionKey) where T : class
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var storageKey = $"{partitionKey}:{key}";
            
            if (_storage.TryGetValue(storageKey, out var item))
            {
                if (item.ExpiresAt.HasValue && item.ExpiresAt.Value < DateTime.UtcNow)
                {
                    _storage.TryRemove(storageKey, out _);
                    UpdateMetrics(false, stopwatch.ElapsedMilliseconds);
                    return null;
                }
                
                if (item.IsDeleted)
                {
                    return null;
                }
                
                UpdateMetrics(true, stopwatch.ElapsedMilliseconds);
                return (T)item.Data!;
            }
            
            UpdateMetrics(false, stopwatch.ElapsedMilliseconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting item from fast storage: {Key}, {PartitionKey}", key, partitionKey);
            throw;
        }
    }

    public async Task<bool> SetAsync<T>(string key, string partitionKey, T value, TimeSpan? ttl = null) where T : class
    {
        try
        {
            var storageKey = $"{partitionKey}:{key}";
            var version = Guid.NewGuid().ToString();
            
            var item = new FastStorageItem<object>
            {
                Key = key,
                PartitionKey = partitionKey,
                Data = value,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null,
                Version = version,
                IsDeleted = false
            };
            
            _storage.AddOrUpdate(storageKey, item, (k, v) => item);
            
            // Update GSIs if it's a survey response
            if (value is FastStorageSurveyResponse surveyResponse)
            {
                UpdateGSIs(surveyResponse, storageKey, item);
            }
            
            _logger.LogDebug("Set item in fast storage: {Key}, {PartitionKey}, Version: {Version}", key, partitionKey, version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting item in fast storage: {Key}, {PartitionKey}", key, partitionKey);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string key, string partitionKey)
    {
        try
        {
            var storageKey = $"{partitionKey}:{key}";
            
            if (_storage.TryGetValue(storageKey, out var item))
            {
                item.IsDeleted = true;
                item.Version = Guid.NewGuid().ToString();
                
                // Remove from GSIs
                RemoveFromGSIs(storageKey);
                
                _logger.LogDebug("Marked item as deleted in fast storage: {Key}, {PartitionKey}", key, partitionKey);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item from fast storage: {Key}, {PartitionKey}", key, partitionKey);
            throw;
        }
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(string partitionKey, string? sortKey = null) where T : class
    {
        try
        {
            var results = new List<T>();
            var prefix = $"{partitionKey}:";
            
            foreach (var kvp in _storage)
            {
                if (kvp.Key.StartsWith(prefix))
                {
                    var item = kvp.Value;
                    
                    // Check if expired
                    if (item.ExpiresAt.HasValue && item.ExpiresAt.Value < DateTime.UtcNow)
                    {
                        continue;
                    }
                    
                    // Check if deleted
                    if (item.IsDeleted)
                    {
                        continue;
                    }
                    
                    // Check sort key if provided
                    if (!string.IsNullOrEmpty(sortKey) && !item.Key.StartsWith(sortKey))
                    {
                        continue;
                    }
                    
                    results.Add((T)item.Data!);
                }
            }
            
            _logger.LogDebug("Query returned {Count} items for partition key: {PartitionKey}", results.Count, partitionKey);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying items from fast storage: {PartitionKey}", partitionKey);
            throw;
        }
    }

    public async Task<bool> BatchWriteAsync<T>(IEnumerable<FastStorageItem<T>> items) where T : class
    {
        try
        {
            var successCount = 0;
            var totalCount = 0;
            
            foreach (var item in items)
            {
                totalCount++;
                var success = await SetAsync(item.Key, item.PartitionKey, item.Data, 
                    item.ExpiresAt.HasValue ? item.ExpiresAt.Value - item.CreatedAt : null);
                
                if (success)
                {
                    successCount++;
                }
            }
            
            _logger.LogInformation("Batch write completed: {SuccessCount}/{TotalCount} items", successCount, totalCount);
            return successCount == totalCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch write operation");
            throw;
        }
    }

    public async Task<FastStorageMetrics> GetMetricsAsync()
    {
        lock (_metricsLock)
        {
            _metrics.TotalItems = _storage.Count;
            _metrics.LastUpdated = DateTime.UtcNow;
            return _metrics;
        }
    }

    public async Task<bool> ExistsAsync(string key, string partitionKey)
    {
        try
        {
            var storageKey = $"{partitionKey}:{key}";
            
            if (_storage.TryGetValue(storageKey, out var item))
            {
                if (item.ExpiresAt.HasValue && item.ExpiresAt.Value < DateTime.UtcNow)
                {
                    return false;
                }
                
                return !item.IsDeleted;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence: {Key}, {PartitionKey}", key, partitionKey);
            throw;
        }
    }

    public async Task<bool> UpdateAsync<T>(string key, string partitionKey, T value, string expectedVersion) where T : class
    {
        try
        {
            var storageKey = $"{partitionKey}:{key}";
            
            if (_storage.TryGetValue(storageKey, out var existingItem))
            {
                if (existingItem.Version != expectedVersion)
                {
                    _logger.LogWarning("Version mismatch for optimistic concurrency: {Key}, Expected: {Expected}, Actual: {Actual}", 
                        key, expectedVersion, existingItem.Version);
                    return false;
                }
                
                var newVersion = Guid.NewGuid().ToString();
                var updatedItem = new FastStorageItem<object>
                {
                    Key = key,
                    PartitionKey = partitionKey,
                    Data = value,
                    CreatedAt = existingItem.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = existingItem.ExpiresAt,
                    Version = newVersion,
                    IsDeleted = false
                };
                
                _storage.AddOrUpdate(storageKey, updatedItem, (k, v) => updatedItem);
                
                // Update GSIs if it's a survey response
                if (value is FastStorageSurveyResponse surveyResponse)
                {
                    UpdateGSIs(surveyResponse, storageKey, updatedItem);
                }
                
                _logger.LogDebug("Updated item with optimistic concurrency: {Key}, {PartitionKey}, Version: {Version}", 
                    key, partitionKey, newVersion);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating item with optimistic concurrency: {Key}, {PartitionKey}", key, partitionKey);
            throw;
        }
    }

    private void UpdateGSIs(FastStorageSurveyResponse surveyResponse, string storageKey, FastStorageItem<object> item)
    {
        // Update SurveyId GSI
        var surveyIdKey = $"{surveyResponse.SurveyId}:{surveyResponse.ResponseId}";
        _gsiSurveyId.AddOrUpdate(surveyIdKey, item, (k, v) => item);
        
        // Update ProcessingStatus GSI
        var statusKey = $"{surveyResponse.ProcessingStatus}:{surveyResponse.ResponseId}";
        _gsiProcessingStatus.AddOrUpdate(statusKey, item, (k, v) => item);
    }

    private void RemoveFromGSIs(string storageKey)
    {
        // Remove from all GSIs
        var surveyIdKeys = _gsiSurveyId.Keys.Where(k => k.EndsWith(storageKey.Split(':').Last())).ToList();
        foreach (var key in surveyIdKeys)
        {
            _gsiSurveyId.TryRemove(key, out _);
        }
        
        var statusKeys = _gsiProcessingStatus.Keys.Where(k => k.EndsWith(storageKey.Split(':').Last())).ToList();
        foreach (var key in statusKeys)
        {
            _gsiProcessingStatus.TryRemove(key, out _);
        }
    }

    private void UpdateMetrics(bool hit, double responseTime)
    {
        lock (_metricsLock)
        {
            if (hit)
            {
                _metrics.CacheHitRate++;
            }
            
            // Update average response time (simple moving average)
            var totalRequests = _metrics.CacheHitRate + (_metrics.TotalItems - _metrics.CacheHitRate);
            if (totalRequests > 0)
            {
                _metrics.AverageResponseTime = ((_metrics.AverageResponseTime * (totalRequests - 1)) + responseTime) / totalRequests;
            }
        }
    }

    private void CleanupExpiredItems(object? state)
    {
        try
        {
            var expiredCount = 0;
            var now = DateTime.UtcNow;
            
            var expiredKeys = _storage.Keys
                .Where(key => _storage.TryGetValue(key, out var item) && 
                             item.ExpiresAt.HasValue && 
                             item.ExpiresAt.Value < now)
                .ToList();
            
            foreach (var key in expiredKeys)
            {
                if (_storage.TryRemove(key, out _))
                {
                    expiredCount++;
                }
            }
            
            if (expiredCount > 0)
            {
                lock (_metricsLock)
                {
                    _metrics.ExpiredItems += expiredCount;
                }
                
                _logger.LogInformation("Cleaned up {ExpiredCount} expired items", expiredCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup of expired items");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
}
