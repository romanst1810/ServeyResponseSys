using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using System.Collections.Concurrent;
using System.Linq;

namespace Survey.Infrastructure.Services;

public class MetricsService : IMetricsService
{
    private readonly ISurveyResponseRepository _repository;
    private readonly IFastStorageRepository _fastStorage;
    private readonly ILogger<MetricsService> _logger;
    private readonly ConcurrentDictionary<string, NpsMetrics> _metricsCache = new();

    public MetricsService(
        ISurveyResponseRepository repository,
        IFastStorageRepository fastStorage,
        ILogger<MetricsService> logger)
    {
        _repository = repository;
        _fastStorage = fastStorage;
        _logger = logger;
    }

    public async Task<NpsMetrics> CalculateNpsMetricsAsync(string clientId, string? surveyId = null, string period = "day")
    {
        try
        {
            _logger.LogInformation("Calculating NPS metrics for client: {ClientId}, survey: {SurveyId}, period: {Period}", 
                clientId, surveyId, period);

            // Get date range based on period
            var (fromDate, toDate) = GetDateRange(period);

            // Get responses for the period
            var responses = await GetResponsesForPeriod(clientId, surveyId, fromDate, toDate);
            var previousResponses = await GetResponsesForPeriod(clientId, surveyId, fromDate.AddDays(-1), toDate.AddDays(-1));

            // Calculate current metrics
            var currentMetrics = CalculateMetrics(responses);
            var previousMetrics = CalculateMetrics(previousResponses);

            // Calculate change
            var change = currentMetrics.AverageNps - previousMetrics.AverageNps;
            var changePercentage = previousMetrics.AverageNps > 0 
                ? (change / previousMetrics.AverageNps) * 100 
                : 0;

            // Determine trend direction
            var direction = change switch
            {
                > 0.1m => "increasing",
                < -0.1m => "decreasing",
                _ => "stable"
            };

            var metrics = new NpsMetrics
            {
                ClientId = clientId,
                SurveyId = surveyId,
                Period = period,
                Metrics = new NpsMetricsData
                {
                    CurrentNps = Math.Round(currentMetrics.AverageNps, 2),
                    PreviousNps = Math.Round(previousMetrics.AverageNps, 2),
                    Change = Math.Round(change, 2),
                    ChangePercentage = Math.Round(changePercentage, 1),
                    TotalResponses = currentMetrics.TotalResponses,
                    ResponseCount = currentMetrics.ResponseCount,
                    SatisfactionDistribution = currentMetrics.SatisfactionDistribution,
                    NpsBreakdown = currentMetrics.NpsBreakdown
                },
                Trends = new TrendInfo
                {
                    Direction = direction,
                    Confidence = CalculateConfidence(responses.Count()),
                    LastUpdated = DateTime.UtcNow
                },
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Calculated NPS metrics for client: {ClientId}, NPS: {Nps}, Responses: {ResponseCount}", 
                clientId, metrics.Metrics.CurrentNps, metrics.Metrics.ResponseCount);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating NPS metrics for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task UpdateMetricsAsync(string clientId, Survey.Core.Models.SurveyResponse surveyResponse)
    {
        try
        {
            _logger.LogInformation("Updating metrics for client: {ClientId}, response: {ResponseId}", 
                clientId, surveyResponse.ResponseId);

            // Invalidate cache for this client
            var cacheKey = GetCacheKey(clientId, surveyResponse.SurveyId);
            _metricsCache.TryRemove(cacheKey, out _);

            // Update fast storage metrics
            var metrics = await CalculateNpsMetricsAsync(clientId, surveyResponse.SurveyId, "day");
            await _fastStorage.UpdateNpsMetricsAsync(clientId, metrics);

            _logger.LogInformation("Metrics updated for client: {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metrics for client: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<NpsMetrics?> GetCachedMetricsAsync(string clientId, string? surveyId = null)
    {
        try
        {
            var cacheKey = GetCacheKey(clientId, surveyId);
            _metricsCache.TryGetValue(cacheKey, out var metrics);

            // Check if cache is still valid (5 minutes)
            if (metrics != null && DateTime.UtcNow.Subtract(metrics.Timestamp).TotalMinutes < 5)
            {
                return metrics;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached metrics for client: {ClientId}", clientId);
            return null;
        }
    }

    public async Task CacheMetricsAsync(string clientId, NpsMetrics metrics)
    {
        try
        {
            var cacheKey = GetCacheKey(clientId, metrics.SurveyId);
            _metricsCache[cacheKey] = metrics;

            _logger.LogInformation("Cached metrics for client: {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching metrics for client: {ClientId}", clientId);
        }
    }

    private async Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetResponsesForPeriod(string clientId, string? surveyId, DateTime fromDate, DateTime toDate)
    {
        var allResponses = await _fastStorage.GetByClientIdAsync(clientId, 0, 1000);
        
        return allResponses.Where(r => 
            r.CreatedAt >= fromDate && 
            r.CreatedAt <= toDate &&
            (surveyId == null || r.SurveyId == surveyId));
    }

    private (DateTime fromDate, DateTime toDate) GetDateRange(string period)
    {
        var now = DateTime.UtcNow;
        
        return period.ToLower() switch
        {
            "hour" => (now.AddHours(-1), now),
            "day" => (now.Date, now),
            "week" => (now.AddDays(-7), now),
            "month" => (now.AddMonths(-1), now),
            _ => (now.Date, now)
        };
    }

    private (decimal AverageNps, int TotalResponses, int ResponseCount, Dictionary<string, int> SatisfactionDistribution, NpsBreakdown NpsBreakdown) CalculateMetrics(IEnumerable<Survey.Core.Models.SurveyResponse> responses)
    {
        var responsesList = responses.ToList();
        var responseCount = responsesList.Count;

        if (responseCount == 0)
        {
            return (0, 0, 0, new Dictionary<string, int>(), new NpsBreakdown());
        }

        // Calculate NPS
        var npsScores = responsesList
            .Where(r => r.Responses.NpsScore.HasValue)
            .Select(r => r.Responses.NpsScore.Value)
            .ToList();

        var averageNps = npsScores.Any() ? npsScores.Average() : 0;

        // Calculate NPS breakdown
        var promoters = npsScores.Count(s => s >= 9);
        var detractors = npsScores.Count(s => s <= 6);
        var passives = npsScores.Count(s => s == 7 || s == 8);

        // Calculate satisfaction distribution
        var satisfactionDistribution = responsesList
            .Where(r => !string.IsNullOrEmpty(r.Responses.Satisfaction))
            .GroupBy(r => r.Responses.Satisfaction)
            .ToDictionary(g => g.Key!, g => g.Count());

        return (
            (decimal)averageNps,
            responseCount,
            responseCount,
            satisfactionDistribution,
            new NpsBreakdown
            {
                Promoters = promoters,
                Passives = passives,
                Detractors = detractors
            }
        );
    }

    private decimal CalculateConfidence(int responseCount)
    {
        // Simple confidence calculation based on response count
        return responseCount switch
        {
            < 10 => 0.5m,
            < 50 => 0.7m,
            < 100 => 0.8m,
            < 500 => 0.9m,
            _ => 0.95m
        };
    }

    private static string GetCacheKey(string clientId, string? surveyId)
    {
        return $"{clientId}:{surveyId ?? "all"}:day";
    }
}
