using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Survey.Core.Models;

public class SurveyResponse
{
    [Required]
    public string SurveyId { get; set; } = string.Empty;
    
    [Required]
    public string ClientId { get; set; } = string.Empty;
    
    [Required]
    public string ResponseId { get; set; } = string.Empty;
    
    public SurveyResponses Responses { get; set; } = new();
    
    public ResponseMetadata Metadata { get; set; } = new();
    
    public string ProcessingStatus { get; set; } = "pending";
    
    public int RetryCount { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SurveyResponses
{
    [Range(0, 10, ErrorMessage = "NPS score must be between 0 and 10")]
    public int? NpsScore { get; set; }
    
    [RegularExpression("^(very_satisfied|satisfied|neutral|dissatisfied|very_dissatisfied)$", 
        ErrorMessage = "Satisfaction must be one of: very_satisfied, satisfied, neutral, dissatisfied, very_dissatisfied")]
    public string? Satisfaction { get; set; }
    
    public Dictionary<string, object> CustomFields { get; set; } = new();
}

public class ResponseMetadata
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string? UserAgent { get; set; }
    
    public string? IpAddress { get; set; }
    
    public string? SessionId { get; set; }
    
    public string? DeviceType { get; set; }
    
    public LocationInfo? Location { get; set; }
}

public class LocationInfo
{
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
}

public class SurveyResponseRequest
{
    [Required]
    public string SurveyId { get; set; } = string.Empty;
    
    [Required]
    public string ClientId { get; set; } = string.Empty;
    
    [Required]
    public string ResponseId { get; set; } = string.Empty;
    
    public SurveyResponses Responses { get; set; } = new();
    
    public ResponseMetadata Metadata { get; set; } = new();
}

public class SurveyResponseResult
{
    public string ResponseId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ProcessingId { get; set; } = string.Empty;
    public string EstimatedProcessingTime { get; set; } = "30 seconds";
    public string FastStorageStatus { get; set; } = string.Empty;
    public string RelationalStorageStatus { get; set; } = string.Empty;
    public long ProcessingTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class NpsMetrics
{
    public string ClientId { get; set; } = string.Empty;
    public string? SurveyId { get; set; }
    public string Period { get; set; } = "day";
    public NpsMetricsData Metrics { get; set; } = new();
    public TrendInfo Trends { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class NpsMetricsData
{
    public decimal CurrentNps { get; set; }
    public decimal PreviousNps { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercentage { get; set; }
    public int TotalResponses { get; set; }
    public int ResponseCount { get; set; }
    public Dictionary<string, int> SatisfactionDistribution { get; set; } = new();
    public NpsBreakdown NpsBreakdown { get; set; } = new();
}

public class NpsBreakdown
{
    public int Promoters { get; set; }
    public int Passives { get; set; }
    public int Detractors { get; set; }
}

public class TrendInfo
{
    public string Direction { get; set; } = "stable";
    public decimal Confidence { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of a dual storage query operation with metadata
/// </summary>
public class DualStorageQueryResult<T>
{
    /// <summary>
    /// The actual data results
    /// </summary>
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();

    /// <summary>
    /// Total count of items across both storages
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Number of items retrieved from fast storage
    /// </summary>
    public int FastStorageCount { get; set; }

    /// <summary>
    /// Number of items retrieved from relational storage
    /// </summary>
    public int RelationalStorageCount { get; set; }

    /// <summary>
    /// Number of unique items after deduplication
    /// </summary>
    public int UniqueCount { get; set; }

    /// <summary>
    /// Number of items that were cache warmed
    /// </summary>
    public int CacheWarmedCount { get; set; }

    /// <summary>
    /// Number of items with potential consistency issues
    /// </summary>
    public int ConsistencyIssuesCount { get; set; }

    /// <summary>
    /// Total operation time in milliseconds
    /// </summary>
    public long OperationTimeMs { get; set; }

    /// <summary>
    /// Fast storage response time in milliseconds
    /// </summary>
    public long FastStorageTimeMs { get; set; }

    /// <summary>
    /// Relational storage response time in milliseconds
    /// </summary>
    public long RelationalStorageTimeMs { get; set; }

    /// <summary>
    /// Whether the operation used fallback strategy
    /// </summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Storage health status
    /// </summary>
    public StorageHealthStatus StorageHealth { get; set; } = new();

    /// <summary>
    /// Any warnings or issues encountered
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Storage health status for dual storage operations
/// </summary>
public class StorageHealthStatus
{
    /// <summary>
    /// Whether fast storage is healthy
    /// </summary>
    public bool FastStorageHealthy { get; set; } = true;

    /// <summary>
    /// Whether relational storage is healthy
    /// </summary>
    public bool RelationalStorageHealthy { get; set; } = true;

    /// <summary>
    /// Fast storage error message if any
    /// </summary>
    public string? FastStorageError { get; set; }

    /// <summary>
    /// Relational storage error message if any
    /// </summary>
    public string? RelationalStorageError { get; set; }
}
