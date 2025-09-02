using System.ComponentModel.DataAnnotations;

namespace Survey.Infrastructure.Data.Entities;

public class AuditLogEntity
{
    [Key]
    public string LogId { get; set; } = string.Empty;
    
    [Required]
    public string ClientId { get; set; } = string.Empty;
    
    public string? SurveyId { get; set; }
    
    public string? ResponseId { get; set; }
    
    [Required]
    public string Action { get; set; } = string.Empty;
    
    [Required]
    public string EntityType { get; set; } = string.Empty;
    
    public string? EntityId { get; set; }
    
    public string? OldValues { get; set; }
    
    public string? NewValues { get; set; }
    
    public string? UserId { get; set; }
    
    public string? IpAddress { get; set; }
    
    public string? UserAgent { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
