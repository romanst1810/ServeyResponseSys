using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Survey.Infrastructure.Services;

public class InMemoryFastStorageService : IFastStorageRepository
{
    private readonly ConcurrentDictionary<string, Survey.Core.Models.SurveyResponse> _responses = new();
    private readonly ConcurrentDictionary<string, NpsMetrics> _metrics = new();
    private readonly ILogger<InMemoryFastStorageService> _logger;

    public InMemoryFastStorageService(ILogger<InMemoryFastStorageService> logger)
    {
        _logger = logger;
    }

    public async Task<Survey.Core.Models.SurveyResponse?> GetByIdAsync(string responseId)
    {
        try
        {
            _responses.TryGetValue(responseId, out var response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting survey response from fast storage: {ResponseId}", responseId);
            throw;
        }
    }

    public async Task<Survey.Core.Models.SurveyResponse> CreateAsync(Survey.Core.Models.SurveyResponse surveyResponse)
    {
        try
        {
            _responses[surveyResponse.ResponseId] = surveyResponse;
            _logger.LogInformation("Created survey response in fast storage: {ResponseId}", surveyResponse.ResponseId);
            return surveyResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating survey response in fast storage: {ResponseId}", surveyResponse.ResponseId);
            throw;
        }
    }

    public async Task<Survey.Core.Models.SurveyResponse> UpdateAsync(Survey.Core.Models.SurveyResponse surveyResponse)
    {
        try
        {
            if (_responses.ContainsKey(surveyResponse.ResponseId))
            {
                _responses[surveyResponse.ResponseId] = surveyResponse;
                _logger.LogInformation("Updated survey response in fast storage: {ResponseId}", surveyResponse.ResponseId);
                return surveyResponse;
            }

            throw new InvalidOperationException($"Survey response not found in fast storage: {surveyResponse.ResponseId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating survey response in fast storage: {ResponseId}", surveyResponse.ResponseId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string responseId)
    {
        try
        {
            var removed = _responses.TryRemove(responseId, out _);
            if (removed)
            {
                _logger.LogInformation("Deleted survey response from fast storage: {ResponseId}", responseId);
            }
            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting survey response from fast storage: {ResponseId}", responseId);
            throw;
        }
    }

    public async Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetByClientIdAsync(string clientId, int skip = 0, int take = 100)
    {
        try
        {
            var responses = _responses.Values
                .Where(r => r.ClientId == clientId)
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting survey responses by client ID from fast storage: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<NpsMetrics?> GetNpsMetricsAsync(string clientId, string? surveyId = null, string period = "day")
    {
        try
        {
            var key = GetMetricsKey(clientId, surveyId, period);
            _metrics.TryGetValue(key, out var metrics);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NPS metrics from fast storage: {ClientId}", clientId);
            throw;
        }
    }

    public async Task UpdateNpsMetricsAsync(string clientId, NpsMetrics metrics)
    {
        try
        {
            var key = GetMetricsKey(clientId, metrics.SurveyId, metrics.Period);
            _metrics[key] = metrics;
            _logger.LogInformation("Updated NPS metrics in fast storage: {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating NPS metrics in fast storage: {ClientId}", clientId);
            throw;
        }
    }

    private static string GetMetricsKey(string clientId, string? surveyId, string period)
    {
        return $"{clientId}:{surveyId ?? "all"}:{period}";
    }
}
