namespace SurveyApi.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public ApiError? Error { get; set; }
    public DateTime Timestamp { get; set; }
    public string? RequestId { get; set; }
}
