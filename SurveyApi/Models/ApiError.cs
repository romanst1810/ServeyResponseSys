namespace SurveyApi.Models;

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<ValidationDetail>? Details { get; set; }
}
