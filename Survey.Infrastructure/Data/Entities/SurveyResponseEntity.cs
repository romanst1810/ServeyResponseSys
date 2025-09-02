using System.ComponentModel.DataAnnotations;

namespace Survey.Infrastructure.Data.Entities;

public class SurveyResponseEntity
{
    [Key]
    public string ResponseId { get; set; } = string.Empty;
    
    [Required]
    public string SurveyId { get; set; } = string.Empty;
    
    [Required]
    public string ClientId { get; set; } = string.Empty;
    
    public int? NpsScore { get; set; }
    
    public string? Satisfaction { get; set; }
    
    public string? CustomFields { get; set; } // JSON field
    
    public string? UserAgent { get; set; }
    
    public string? IpAddress { get; set; }
    
    public string? SessionId { get; set; }
    
    public string? DeviceType { get; set; }
    
    public string ProcessingStatus { get; set; } = "pending";
    
    public int RetryCount { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
