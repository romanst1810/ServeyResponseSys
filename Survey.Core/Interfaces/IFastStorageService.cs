using Survey.Core.Models;

namespace Survey.Core.Interfaces;

/// <summary>
/// Advanced fast storage service interface with DynamoDB-like features
/// </summary>
public interface IFastStorageService
{
    /// <summary>
    /// Get an item by key and partition key
    /// </summary>
    Task<T?> GetAsync<T>(string key, string partitionKey) where T : class;
    
    /// <summary>
    /// Set an item with optional TTL
    /// </summary>
    Task<bool> SetAsync<T>(string key, string partitionKey, T value, TimeSpan? ttl = null) where T : class;
    
    /// <summary>
    /// Delete an item by key and partition key
    /// </summary>
    Task<bool> DeleteAsync(string key, string partitionKey);
    
    /// <summary>
    /// Query items by partition key with optional sort key
    /// </summary>
    Task<IEnumerable<T>> QueryAsync<T>(string partitionKey, string? sortKey = null) where T : class;
    
    /// <summary>
    /// Batch write multiple items
    /// </summary>
    Task<bool> BatchWriteAsync<T>(IEnumerable<FastStorageItem<T>> items) where T : class;
    
    /// <summary>
    /// Get storage metrics
    /// </summary>
    Task<FastStorageMetrics> GetMetricsAsync();
    
    /// <summary>
    /// Check if an item exists
    /// </summary>
    Task<bool> ExistsAsync(string key, string partitionKey);
    
    /// <summary>
    /// Update an item with optimistic concurrency control
    /// </summary>
    Task<bool> UpdateAsync<T>(string key, string partitionKey, T value, string expectedVersion) where T : class;
}

/// <summary>
/// Fast storage item with metadata
/// </summary>
public class FastStorageItem<T>
{
    public string Key { get; set; } = string.Empty;
    public string PartitionKey { get; set; } = string.Empty;
    public string? SortKey { get; set; }
    public T? Data { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Version { get; set; } = string.Empty; // Optimistic concurrency
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Fast storage metrics
/// </summary>
public class FastStorageMetrics
{
    public int TotalItems { get; set; }
    public double AverageResponseTime { get; set; }
    public int CacheHitRate { get; set; }
    public int ExpiredItems { get; set; }
    public int ActiveConnections { get; set; }
    public DateTime LastUpdated { get; set; }
}
