using Survey.Core.Models;

namespace Survey.Core.Interfaces;

/// <summary>
/// Storage orchestrator for managing dual storage operations
/// </summary>
public interface IStorageOrchestrator
{
    /// <summary>
    /// Process survey response with dual storage strategy
    /// </summary>
    Task<SurveyResponseResult> ProcessSurveyResponseAsync(SurveyResponseRequest request);
    
    /// <summary>
    /// Get survey response with cache-first strategy
    /// </summary>
    Task<SurveyResponse?> GetSurveyResponseAsync(string responseId, string clientId);
    
    /// <summary>
    /// Update survey response in both storages
    /// </summary>
    Task<SurveyResponse> UpdateSurveyResponseAsync(SurveyResponse surveyResponse);
    
    /// <summary>
    /// Delete survey response from both storages
    /// </summary>
    Task<bool> DeleteSurveyResponseAsync(string responseId, string clientId);
    
    /// <summary>
    /// Get responses by client with optimized strategy
    /// </summary>
    Task<IEnumerable<SurveyResponse>> GetResponsesByClientAsync(string clientId, int skip = 0, int take = 100);

    /// <summary>
    /// Get survey responses by client ID with detailed dual storage metadata
    /// </summary>
    Task<DualStorageQueryResult<SurveyResponse>> GetResponsesByClientWithMetadataAsync(string clientId, int skip = 0, int take = 100);
    
    /// <summary>
    /// Check consistency between fast and relational storage
    /// </summary>
    Task<bool> CheckConsistencyAsync(string responseId, string clientId);
    
    /// <summary>
    /// Get storage health metrics
    /// </summary>
    Task<StorageHealthMetrics> GetStorageHealthAsync();
    
    /// <summary>
    /// Force sync between storages
    /// </summary>
    Task<bool> ForceSyncAsync(string responseId, string clientId);
}

/// <summary>
/// Storage health metrics
/// </summary>
public class StorageHealthMetrics
{
    public FastStorageHealth FastStorage { get; set; } = new();
    public RelationalStorageHealth RelationalStorage { get; set; } = new();
    public SyncHealth Sync { get; set; } = new();
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}

public class FastStorageHealth
{
    public bool IsHealthy { get; set; }
    public double ResponseTime { get; set; }
    public int TotalItems { get; set; }
    public int CacheHitRate { get; set; }
    public string? ErrorMessage { get; set; }
}

public class RelationalStorageHealth
{
    public bool IsHealthy { get; set; }
    public double ResponseTime { get; set; }
    public int TotalRecords { get; set; }
    public int ActiveConnections { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SyncHealth
{
    public bool IsHealthy { get; set; }
    public int PendingItems { get; set; }
    public int FailedItems { get; set; }
    public double AverageSyncTime { get; set; }
    public string? ErrorMessage { get; set; }
}
