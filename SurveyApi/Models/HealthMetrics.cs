namespace SurveyApi.Models
{
    public class HealthMetrics
    {
        public int RequestsPerSecond { get; set; }
        public string AverageResponseTime { get; set; } = string.Empty;
        public decimal ErrorRate { get; set; }
    }
}
