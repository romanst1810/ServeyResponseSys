using Survey.Core.Interfaces;

namespace SurveyApi.Models;

public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, ComponentStatus> Components { get; set; } = new();
    public HealthMetrics Metrics { get; set; } = new();
    public StorageHealthMetrics? Storage { get; set; }
}
