using System.ComponentModel.DataAnnotations;

namespace Survey.Infrastructure.Data.Entities;

public class ClientMetricsEntity
{
    [Key]
    public string MetricId { get; set; } = string.Empty;
    
    [Required]
    public string ClientId { get; set; } = string.Empty;
    
    public string? SurveyId { get; set; }
    
    [Required]
    public DateTime MetricDate { get; set; }
    
    public int TotalResponses { get; set; } = 0;
    
    public decimal? AverageNps { get; set; }
    
    public string? SatisfactionDistribution { get; set; }
    
    public decimal? ResponseRate { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
