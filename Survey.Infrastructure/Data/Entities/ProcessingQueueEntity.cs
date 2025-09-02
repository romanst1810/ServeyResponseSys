using System.ComponentModel.DataAnnotations;

namespace Survey.Infrastructure.Data.Entities;

public class ProcessingQueueEntity
{
    [Key]
    public string ProcessingId { get; set; } = string.Empty;
    
    [Required]
    public string ResponseId { get; set; } = string.Empty;
    
    [Required]
    public string ClientId { get; set; } = string.Empty;
    
    [Required]
    public string SurveyId { get; set; } = string.Empty;
    
    [Required]
    public string Status { get; set; } = "pending";
    
    public int RetryCount { get; set; } = 0;
    
    public int MaxRetries { get; set; } = 3;
    
    public string? ErrorMessage { get; set; }
    
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ProcessedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
