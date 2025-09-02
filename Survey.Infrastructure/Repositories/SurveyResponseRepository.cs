using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using Survey.Infrastructure.Data;
using Survey.Infrastructure.Data.Entities;
using System.Text.Json;

namespace Survey.Infrastructure.Repositories;

public class SurveyResponseRepository : ISurveyResponseRepository
{
    private readonly SurveyResponseDbContext _context;
    private readonly ILogger<SurveyResponseRepository> _logger;

    public SurveyResponseRepository(SurveyResponseDbContext context, ILogger<SurveyResponseRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Survey.Core.Models.SurveyResponse?> GetByIdAsync(string responseId)
    {
        try
        {
            var entity = await _context.SurveyResponses
                .FirstOrDefaultAsync(e => e.ResponseId == responseId);

            return entity != null ? MapToModel(entity) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting survey response by ID: {ResponseId}", responseId);
            throw;
        }
    }

    public async Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetByClientIdAsync(string clientId, int skip = 0, int take = 100)
    {
        try
        {
            var entities = await _context.SurveyResponses
                .Where(e => e.ClientId == clientId)
                .OrderByDescending(e => e.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return entities.Select(MapToModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting survey responses by client ID: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<IEnumerable<Survey.Core.Models.SurveyResponse>> GetBySurveyIdAsync(string surveyId, int skip = 0, int take = 100)
    {
        try
        {
            var entities = await _context.SurveyResponses
                .Where(e => e.SurveyId == surveyId)
                .OrderByDescending(e => e.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return entities.Select(MapToModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting survey responses by survey ID: {SurveyId}", surveyId);
            throw;
        }
    }

    public async Task<Survey.Core.Models.SurveyResponse> CreateAsync(Survey.Core.Models.SurveyResponse surveyResponse)
    {
        try
        {
            var entity = MapToEntity(surveyResponse);
            _context.SurveyResponses.Add(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created survey response: {ResponseId}", surveyResponse.ResponseId);
            return MapToModel(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating survey response: {ResponseId}", surveyResponse.ResponseId);
            throw;
        }
    }

    public async Task<Survey.Core.Models.SurveyResponse> UpdateAsync(Survey.Core.Models.SurveyResponse surveyResponse)
    {
        try
        {
            var entity = await _context.SurveyResponses
                .FirstOrDefaultAsync(e => e.ResponseId == surveyResponse.ResponseId);

            if (entity == null)
                throw new InvalidOperationException($"Survey response not found: {surveyResponse.ResponseId}");

            UpdateEntity(entity, surveyResponse);
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated survey response: {ResponseId}", surveyResponse.ResponseId);
            return MapToModel(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating survey response: {ResponseId}", surveyResponse.ResponseId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string responseId)
    {
        try
        {
            var entity = await _context.SurveyResponses
                .FirstOrDefaultAsync(e => e.ResponseId == responseId);

            if (entity == null)
                return false;

            _context.SurveyResponses.Remove(entity);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted survey response: {ResponseId}", responseId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting survey response: {ResponseId}", responseId);
            throw;
        }
    }

    public async Task<int> GetCountByClientIdAsync(string clientId)
    {
        try
        {
            return await _context.SurveyResponses
                .CountAsync(e => e.ClientId == clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting count by client ID: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<int> GetCountBySurveyIdAsync(string surveyId)
    {
        try
        {
            return await _context.SurveyResponses
                .CountAsync(e => e.SurveyId == surveyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting count by survey ID: {SurveyId}", surveyId);
            throw;
        }
    }

    public async Task<decimal> GetAverageNpsByClientIdAsync(string clientId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.SurveyResponses
                .Where(e => e.ClientId == clientId && e.NpsScore.HasValue);

            if (fromDate.HasValue)
                query = query.Where(e => e.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.CreatedAt <= toDate.Value);

            var average = await query.AverageAsync(e => e.NpsScore.Value);
            return (decimal)Math.Round(average, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting average NPS by client ID: {ClientId}", clientId);
            throw;
        }
    }

    public async Task<decimal> GetAverageNpsBySurveyIdAsync(string surveyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var query = _context.SurveyResponses
                .Where(e => e.SurveyId == surveyId && e.NpsScore.HasValue);

            if (fromDate.HasValue)
                query = query.Where(e => e.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.CreatedAt <= toDate.Value);

            var average = await query.AverageAsync(e => e.NpsScore.Value);
            return (decimal)Math.Round(average, 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting average NPS by survey ID: {SurveyId}", surveyId);
            throw;
        }
    }

    private static SurveyResponseEntity MapToEntity(Survey.Core.Models.SurveyResponse model)
    {
        return new SurveyResponseEntity
        {
            ResponseId = model.ResponseId,
            SurveyId = model.SurveyId,
            ClientId = model.ClientId,
            NpsScore = model.Responses.NpsScore,
            Satisfaction = model.Responses.Satisfaction,
            CustomFields = JsonSerializer.Serialize(model.Responses.CustomFields),
            UserAgent = model.Metadata.UserAgent,
            IpAddress = model.Metadata.IpAddress,
            SessionId = model.Metadata.SessionId,
            DeviceType = model.Metadata.DeviceType,
            ProcessingStatus = model.ProcessingStatus,
            RetryCount = model.RetryCount,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt
        };
    }

    private static Survey.Core.Models.SurveyResponse MapToModel(SurveyResponseEntity entity)
    {
        return new Survey.Core.Models.SurveyResponse
        {
            ResponseId = entity.ResponseId,
            SurveyId = entity.SurveyId,
            ClientId = entity.ClientId,
            Responses = new SurveyResponses
            {
                NpsScore = entity.NpsScore,
                Satisfaction = entity.Satisfaction,
                CustomFields = !string.IsNullOrEmpty(entity.CustomFields) 
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(entity.CustomFields) ?? new()
                    : new()
            },
            Metadata = new ResponseMetadata
            {
                Timestamp = entity.CreatedAt,
                UserAgent = entity.UserAgent,
                IpAddress = entity.IpAddress,
                SessionId = entity.SessionId,
                DeviceType = entity.DeviceType
            },
            ProcessingStatus = entity.ProcessingStatus,
            RetryCount = entity.RetryCount,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static void UpdateEntity(SurveyResponseEntity entity, Survey.Core.Models.SurveyResponse model)
    {
        entity.NpsScore = model.Responses.NpsScore;
        entity.Satisfaction = model.Responses.Satisfaction;
        entity.CustomFields = JsonSerializer.Serialize(model.Responses.CustomFields);
        entity.UserAgent = model.Metadata.UserAgent;
        entity.IpAddress = model.Metadata.IpAddress;
        entity.SessionId = model.Metadata.SessionId;
        entity.DeviceType = model.Metadata.DeviceType;
        entity.ProcessingStatus = model.ProcessingStatus;
        entity.RetryCount = model.RetryCount;
    }
}
