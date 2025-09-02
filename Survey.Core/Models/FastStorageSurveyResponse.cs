using System.Text.Json;

namespace Survey.Core.Models;

/// <summary>
/// Denormalized survey response optimized for fast storage
/// </summary>
public class FastStorageSurveyResponse
{
    // Primary Key Structure
    public string ClientId { get; set; } = string.Empty;        // Partition Key
    public string ResponseId { get; set; } = string.Empty;      // Sort Key
    
    // Core Data (Denormalized for Performance)
    public string SurveyId { get; set; } = string.Empty;
    public int? NpsScore { get; set; }
    public string? Satisfaction { get; set; }
    public Dictionary<string, object> CustomFields { get; set; } = new();
    
    // Metadata
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? SessionId { get; set; }
    public string? DeviceType { get; set; }
    public string ProcessingStatus { get; set; } = "pending";
    
    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    
    // Version Control
    public string Version { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    
    // Additional Fast Storage Optimizations
    public string? SurveyTitle { get; set; }  // Denormalized for queries
    public string? ClientName { get; set; }   // Denormalized for queries
    public string? DeviceCategory { get; set; } // mobile, desktop, tablet
    public string? Location { get; set; }     // Country/Region for analytics
    
    /// <summary>
    /// Convert to JSON for storage
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
    
    /// <summary>
    /// Create from JSON
    /// </summary>
    public static FastStorageSurveyResponse FromJson(string json)
    {
        return JsonSerializer.Deserialize<FastStorageSurveyResponse>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }) ?? new FastStorageSurveyResponse();
    }
    
    /// <summary>
    /// Create from regular SurveyResponse
    /// </summary>
    public static FastStorageSurveyResponse FromSurveyResponse(SurveyResponse response)
    {
        return new FastStorageSurveyResponse
        {
            ClientId = response.ClientId,
            ResponseId = response.ResponseId,
            SurveyId = response.SurveyId,
            NpsScore = response.Responses?.NpsScore,
            Satisfaction = response.Responses?.Satisfaction,
            CustomFields = response.Responses?.CustomFields ?? new(),
            UserAgent = response.Metadata?.UserAgent,
            IpAddress = response.Metadata?.IpAddress,
            SessionId = response.Metadata?.SessionId,
            DeviceType = response.Metadata?.DeviceType,
            ProcessingStatus = response.ProcessingStatus,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
            Version = Guid.NewGuid().ToString(),
            IsDeleted = false
        };
    }
    
    /// <summary>
    /// Convert back to regular SurveyResponse
    /// </summary>
    public SurveyResponse ToSurveyResponse()
    {
        return new SurveyResponse
        {
            SurveyId = SurveyId,
            ClientId = ClientId,
            ResponseId = ResponseId,
            Responses = new SurveyResponses
            {
                NpsScore = NpsScore,
                Satisfaction = Satisfaction,
                CustomFields = CustomFields
            },
            Metadata = new ResponseMetadata
            {
                UserAgent = UserAgent,
                IpAddress = IpAddress,
                SessionId = SessionId,
                DeviceType = DeviceType
            },
            ProcessingStatus = ProcessingStatus,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
