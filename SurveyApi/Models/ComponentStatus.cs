namespace SurveyApi.Models
{
    public class ComponentStatus
    {
        public string Status { get; set; } = string.Empty;
        public string? ResponseTime { get; set; }
        public int? PendingItems { get; set; }
    }
}
