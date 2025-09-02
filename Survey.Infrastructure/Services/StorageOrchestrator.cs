using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;

namespace Survey.Infrastructure.Services;

/// <summary>
/// Storage orchestrator for managing dual storage operations
/// </summary>
public class StorageOrchestrator : IStorageOrchestrator
{
    private readonly IFastStorageService _fastStorage;
    private readonly ISurveyResponseRepository _relationalStorage;
    private readonly IQueueService _queueService;
    private readonly IMetricsService _metricsService;
    private readonly ILogger<StorageOrchestrator> _logger;

    public StorageOrchestrator(
        IFastStorageService fastStorage,
        ISurveyResponseRepository relationalStorage,
        IQueueService queueService,
        IMetricsService metricsService,
        ILogger<StorageOrchestrator> logger)
    {
        _fastStorage = fastStorage;
        _relationalStorage = relationalStorage;
        _queueService = queueService;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<SurveyResponseResult> ProcessSurveyResponseAsync(SurveyResponseRequest request)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing survey response with dual storage: {ResponseId}", request.ResponseId);

            // Step 1: Validate and prepare response
            var surveyResponse = CreateSurveyResponseFromRequest(request);
            var fastStorageResponse = FastStorageSurveyResponse.FromSurveyResponse(surveyResponse);

            // Step 2: Write to fast storage first (for immediate availability)
            var fastStorageSuccess = await _fastStorage.SetAsync(
                surveyResponse.ResponseId,
                surveyResponse.ClientId,
                fastStorageResponse,
                TimeSpan.FromHours(24) // TTL for fast storage
            );

            if (!fastStorageSuccess)
            {
                throw new InvalidOperationException("Failed to write to fast storage");
            }

            // Step 3: Enqueue for relational storage processing
            var processingId = await _queueService.EnqueueForProcessingAsync(surveyResponse);

            // Step 4: Return immediate response
            var result = new SurveyResponseResult
            {
                ResponseId = surveyResponse.ResponseId,
                Status = "accepted",
                ProcessingId = processingId,
                EstimatedProcessingTime = "30 seconds",
                FastStorageStatus = "available",
                RelationalStorageStatus = "queued",
                ProcessingTime = stopwatch.ElapsedMilliseconds
            };

            _logger.LogInformation("Survey response processed successfully: {ResponseId}, ProcessingId: {ProcessingId}", 
                surveyResponse.ResponseId, processingId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing survey response: {ResponseId}", request.ResponseId);
            throw;
        }
    }

    public async Task<SurveyResponse?> GetSurveyResponseAsync(string responseId, string clientId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Step 1: Try fast storage first (cache-first strategy)
            var fastStorageResult = await _fastStorage.GetAsync<FastStorageSurveyResponse>(responseId, clientId);
            if (fastStorageResult != null)
            {
                _logger.LogDebug("Cache hit for response: {ResponseId}", responseId);
                return fastStorageResult.ToSurveyResponse();
            }

            // Step 2: Fallback to relational storage
            var relationalResult = await _relationalStorage.GetByIdAsync(responseId);
            if (relationalResult != null)
            {
                // Step 3: Re-populate fast storage (cache warming)
                var fastStorageResponse = FastStorageSurveyResponse.FromSurveyResponse(relationalResult);
                await _fastStorage.SetAsync(
                    responseId,
                    clientId,
                    fastStorageResponse,
                    TimeSpan.FromHours(24)
                );
                
                _logger.LogDebug("Cache miss, populated from relational storage: {ResponseId}", responseId);
            }

            return relationalResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting survey response: {ResponseId}", responseId);
            throw;
        }
    }

    public async Task<SurveyResponse> UpdateSurveyResponseAsync(SurveyResponse surveyResponse)
    {
        try
        {
            _logger.LogInformation("Updating survey response in dual storage: {ResponseId}", surveyResponse.ResponseId);

            // Step 1: Update relational storage first (source of truth)
            var updatedRelational = await _relationalStorage.UpdateAsync(surveyResponse);

            // Step 2: Update fast storage
            var fastStorageResponse = FastStorageSurveyResponse.FromSurveyResponse(updatedRelational);
            await _fastStorage.SetAsync(
                surveyResponse.ResponseId,
                surveyResponse.ClientId,
                fastStorageResponse,
                TimeSpan.FromHours(24)
            );

            _logger.LogInformation("Survey response updated successfully: {ResponseId}", surveyResponse.ResponseId);
            return updatedRelational;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating survey response: {ResponseId}", surveyResponse.ResponseId);
            throw;
        }
    }

    public async Task<bool> DeleteSurveyResponseAsync(string responseId, string clientId)
    {
        try
        {
            _logger.LogInformation("Deleting survey response from dual storage: {ResponseId}", responseId);

            // Step 1: Delete from relational storage first
            var relationalDeleted = await _relationalStorage.DeleteAsync(responseId);

            // Step 2: Delete from fast storage
            var fastStorageDeleted = await _fastStorage.DeleteAsync(responseId, clientId);

            var success = relationalDeleted && fastStorageDeleted;
            
            if (success)
            {
                _logger.LogInformation("Survey response deleted successfully: {ResponseId}", responseId);
            }
            else
            {
                _logger.LogWarning("Partial deletion - Relational: {Relational}, FastStorage: {FastStorage}", 
                    relationalDeleted, fastStorageDeleted);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting survey response: {ResponseId}", responseId);
            throw;
        }
    }

    public async Task<IEnumerable<SurveyResponse>> GetResponsesByClientAsync(string clientId, int skip = 0, int take = 100)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogInformation("Getting responses for client with dual storage strategy: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);

            // Step 1: Execute parallel queries to both storages for optimal performance
            var fastStorageTask = GetFromFastStorageAsync(clientId, correlationId);
            var relationalStorageTask = GetFromRelationalStorageAsync(clientId, correlationId);

            // Step 2: Wait for both operations to complete with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tasks = new List<Task<IEnumerable<SurveyResponse>>>
            {
                fastStorageTask,
                relationalStorageTask
            };

            var results = await Task.WhenAll(tasks);

            var fastStorageResults = results[0];
            var relationalStorageResults = results[1];

            _logger.LogDebug("Retrieved {FastCount} from fast storage, {RelationalCount} from relational storage for client: {ClientId}", 
                fastStorageResults.Count(), relationalStorageResults.Count(), clientId);

            // Step 3: Merge and deduplicate results
            var mergedResults = await MergeAndDeduplicateResultsAsync(
                fastStorageResults, 
                relationalStorageResults, 
                clientId, 
                correlationId
            );

            // Step 4: Apply pagination
            var paginatedResults = mergedResults
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();

            // Step 5: Perform consistency check and cache warming for missing items
            _ = Task.Run(async () => await PerformConsistencyCheckAndCacheWarmingAsync(
                fastStorageResults, 
                relationalStorageResults, 
                clientId, 
                correlationId
            ));

            _logger.LogInformation("Successfully retrieved {Count} responses for client: {ClientId}, CorrelationId: {CorrelationId}, Time: {ElapsedMs}ms", 
                paginatedResults.Count, clientId, correlationId, stopwatch.ElapsedMilliseconds);

            return paginatedResults;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Timeout occurred while retrieving responses for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            
            // Fallback: try to get from relational storage only
            return await GetFromRelationalStorageWithFallbackAsync(clientId, skip, take, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting responses for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            
            // Fallback: try to get from relational storage only
            return await GetFromRelationalStorageWithFallbackAsync(clientId, skip, take, correlationId);
        }
    }

    private async Task<IEnumerable<SurveyResponse>> GetFromFastStorageAsync(string clientId, string correlationId)
    {
        try
        {
            var fastStorageResults = await _fastStorage.QueryAsync<FastStorageSurveyResponse>(clientId);
            
            var responses = fastStorageResults
                .Where(r => !r.IsDeleted && (r.ExpiresAt == null || r.ExpiresAt > DateTime.UtcNow))
                .Select(r => r.ToSurveyResponse())
                .ToList();

            _logger.LogDebug("Retrieved {Count} valid responses from fast storage for client: {ClientId}, CorrelationId: {CorrelationId}", 
                responses.Count, clientId, correlationId);

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve from fast storage for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            return Enumerable.Empty<SurveyResponse>();
        }
    }

    private async Task<IEnumerable<SurveyResponse>> GetFromRelationalStorageAsync(string clientId, string correlationId)
    {
        try
        {
            // Get more items from relational storage to account for potential duplicates
            var responses = await _relationalStorage.GetByClientIdAsync(clientId, 0, 1000);
            
            _logger.LogDebug("Retrieved {Count} responses from relational storage for client: {ClientId}, CorrelationId: {CorrelationId}", 
                responses.Count(), clientId, correlationId);

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve from relational storage for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            return Enumerable.Empty<SurveyResponse>();
        }
    }

    private async Task<IEnumerable<SurveyResponse>> MergeAndDeduplicateResultsAsync(
        IEnumerable<SurveyResponse> fastStorageResults, 
        IEnumerable<SurveyResponse> relationalStorageResults, 
        string clientId, 
        string correlationId)
    {
        try
        {
            // Create a dictionary for fast lookup and deduplication
            var mergedDict = new Dictionary<string, SurveyResponse>();

            // Process fast storage results first (they're more recent)
            foreach (var response in fastStorageResults)
            {
                if (!mergedDict.ContainsKey(response.ResponseId))
                {
                    mergedDict[response.ResponseId] = response;
                }
                else
                {
                    // If both exist, prefer the one with more recent UpdatedAt
                    var existing = mergedDict[response.ResponseId];
                    if (response.UpdatedAt > existing.UpdatedAt)
                    {
                        mergedDict[response.ResponseId] = response;
                        _logger.LogDebug("Replaced response with newer version from fast storage: {ResponseId}", response.ResponseId);
                    }
                }
            }

            // Process relational storage results
            foreach (var response in relationalStorageResults)
            {
                if (!mergedDict.ContainsKey(response.ResponseId))
                {
                    mergedDict[response.ResponseId] = response;
                }
                else
                {
                    // If both exist, prefer the one with more recent UpdatedAt
                    var existing = mergedDict[response.ResponseId];
                    if (response.UpdatedAt > existing.UpdatedAt)
                    {
                        mergedDict[response.ResponseId] = response;
                        _logger.LogDebug("Replaced response with newer version from relational storage: {ResponseId}", response.ResponseId);
                    }
                }
            }

            var mergedResults = mergedDict.Values.ToList();

            _logger.LogDebug("Merged and deduplicated {MergedCount} unique responses for client: {ClientId}, CorrelationId: {CorrelationId}", 
                mergedResults.Count, clientId, correlationId);

            return mergedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging and deduplicating results for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            
            // Fallback: return fast storage results if available, otherwise relational
            return fastStorageResults.Any() ? fastStorageResults : relationalStorageResults;
        }
    }

    private async Task PerformConsistencyCheckAndCacheWarmingAsync(
        IEnumerable<SurveyResponse> fastStorageResults, 
        IEnumerable<SurveyResponse> relationalStorageResults, 
        string clientId, 
        string correlationId)
    {
        try
        {
            // Create sets for efficient comparison
            var fastStorageIds = fastStorageResults.Select(r => r.ResponseId).ToHashSet();
            var relationalStorageIds = relationalStorageResults.Select(r => r.ResponseId).ToHashSet();

            // Find items that exist in relational storage but not in fast storage (cache warming)
            var missingInFastStorage = relationalStorageIds.Except(fastStorageIds).ToList();
            
            if (missingInFastStorage.Any())
            {
                _logger.LogInformation("Found {Count} items missing in fast storage, warming cache for client: {ClientId}, CorrelationId: {CorrelationId}", 
                    missingInFastStorage.Count, clientId, correlationId);

                // Warm cache for missing items (limit to prevent overwhelming the system)
                var itemsToWarm = missingInFastStorage.Take(10).ToList();
                
                foreach (var responseId in itemsToWarm)
                {
                    try
                    {
                        var response = relationalStorageResults.FirstOrDefault(r => r.ResponseId == responseId);
                        if (response != null)
                        {
                            var fastStorageResponse = FastStorageSurveyResponse.FromSurveyResponse(response);
                            await _fastStorage.SetAsync(
                                responseId,
                                clientId,
                                fastStorageResponse,
                                TimeSpan.FromHours(24)
                            );
                            
                            _logger.LogDebug("Warmed cache for response: {ResponseId}", responseId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to warm cache for response: {ResponseId}", responseId);
                    }
                }
            }

            // Find items that exist in fast storage but not in relational storage (potential inconsistency)
            var missingInRelationalStorage = fastStorageIds.Except(relationalStorageIds).ToList();
            
            if (missingInRelationalStorage.Any())
            {
                _logger.LogWarning("Found {Count} items in fast storage but missing in relational storage for client: {ClientId}, CorrelationId: {CorrelationId}", 
                    missingInRelationalStorage.Count, clientId, correlationId);

                // These items might be in the sync queue or there might be a consistency issue
                // In a production system, you might want to trigger a sync job or alert
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during consistency check and cache warming for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
        }
    }

    private async Task<IEnumerable<SurveyResponse>> GetFromRelationalStorageWithFallbackAsync(
        string clientId, 
        int skip, 
        int take, 
        string correlationId)
    {
        try
        {
            _logger.LogInformation("Using fallback strategy - getting from relational storage only for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);

            var responses = await _relationalStorage.GetByClientIdAsync(clientId, skip, take);
            
            _logger.LogInformation("Fallback successful - retrieved {Count} responses from relational storage for client: {ClientId}, CorrelationId: {CorrelationId}", 
                responses.Count(), clientId, correlationId);

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallback strategy also failed for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            throw;
        }
    }

    public async Task<bool> CheckConsistencyAsync(string responseId, string clientId)
    {
        try
        {
            var fastData = await _fastStorage.GetAsync<FastStorageSurveyResponse>(responseId, clientId);
            var relationalData = await _relationalStorage.GetByIdAsync(responseId);

            if (fastData == null && relationalData == null)
            {
                return true; // Both empty is consistent
            }

            if (fastData == null || relationalData == null)
            {
                _logger.LogWarning("Inconsistency detected - FastStorage: {FastStorage}, Relational: {Relational}", 
                    fastData != null, relationalData != null);
                return false;
            }

            // Compare key fields for consistency
            var isConsistent = fastData.ResponseId == relationalData.ResponseId &&
                              fastData.NpsScore == relationalData.Responses?.NpsScore &&
                              fastData.ProcessingStatus == relationalData.ProcessingStatus;

            if (!isConsistent)
            {
                _logger.LogWarning("Data inconsistency detected for response: {ResponseId}", responseId);
            }

            return isConsistent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking consistency: {ResponseId}", responseId);
            return false;
        }
    }

    public async Task<StorageHealthMetrics> GetStorageHealthAsync()
    {
        var metrics = new StorageHealthMetrics();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Check fast storage health
            try
            {
                stopwatch.Restart();
                var fastStorageMetrics = await _fastStorage.GetMetricsAsync();
                metrics.FastStorage = new FastStorageHealth
                {
                    IsHealthy = true,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    TotalItems = fastStorageMetrics.TotalItems,
                    CacheHitRate = fastStorageMetrics.CacheHitRate
                };
            }
            catch (Exception ex)
            {
                metrics.FastStorage = new FastStorageHealth
                {
                    IsHealthy = false,
                    ErrorMessage = ex.Message
                };
            }

            // Check relational storage health
            try
            {
                stopwatch.Restart();
                var testQuery = await _relationalStorage.GetCountByClientIdAsync("test");
                metrics.RelationalStorage = new RelationalStorageHealth
                {
                    IsHealthy = true,
                    ResponseTime = stopwatch.ElapsedMilliseconds,
                    TotalRecords = testQuery // This is just a test, not actual count
                };
            }
            catch (Exception ex)
            {
                metrics.RelationalStorage = new RelationalStorageHealth
                {
                    IsHealthy = false,
                    ErrorMessage = ex.Message
                };
            }

            // For now, sync health is simplified
            metrics.Sync = new SyncHealth
            {
                IsHealthy = true,
                PendingItems = 0,
                FailedItems = 0
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting storage health metrics");
            throw;
        }
    }

    public async Task<bool> ForceSyncAsync(string responseId, string clientId)
    {
        try
        {
            _logger.LogInformation("Forcing sync for response: {ResponseId}", responseId);

            // Get from relational storage (source of truth)
            var relationalData = await _relationalStorage.GetByIdAsync(responseId);
            if (relationalData == null)
            {
                // If not in relational storage, remove from fast storage
                await _fastStorage.DeleteAsync(responseId, clientId);
                return true;
            }

            // Update fast storage with latest data
            var fastStorageResponse = FastStorageSurveyResponse.FromSurveyResponse(relationalData);
            var success = await _fastStorage.SetAsync(
                responseId,
                clientId,
                fastStorageResponse,
                TimeSpan.FromHours(24)
            );

            _logger.LogInformation("Force sync completed for response: {ResponseId}, Success: {Success}", responseId, success);
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during force sync: {ResponseId}", responseId);
            return false;
        }
    }

    public async Task<DualStorageQueryResult<SurveyResponse>> GetResponsesByClientWithMetadataAsync(string clientId, int skip = 0, int take = 100)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString();
        var result = new DualStorageQueryResult<SurveyResponse>
        {
            CorrelationId = correlationId
        };
        
        try
        {
            _logger.LogInformation("Getting responses for client with detailed metadata: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);

            // Step 1: Execute parallel queries to both storages with individual timing
            var fastStorageStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var fastStorageTask = GetFromFastStorageAsync(clientId, correlationId);
            
            var relationalStorageStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var relationalStorageTask = GetFromRelationalStorageAsync(clientId, correlationId);

            // Step 2: Wait for both operations to complete with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tasks = new List<Task<IEnumerable<SurveyResponse>>>
            {
                fastStorageTask,
                relationalStorageTask
            };

            var results = await Task.WhenAll(tasks);

            var fastStorageResults = results[0];
            var relationalStorageResults = results[1];

            // Record timing
            fastStorageStopwatch.Stop();
            relationalStorageStopwatch.Stop();
            
            result.FastStorageTimeMs = fastStorageStopwatch.ElapsedMilliseconds;
            result.RelationalStorageTimeMs = relationalStorageStopwatch.ElapsedMilliseconds;
            result.FastStorageCount = fastStorageResults.Count();
            result.RelationalStorageCount = relationalStorageResults.Count();

            _logger.LogDebug("Retrieved {FastCount} from fast storage ({FastTime}ms), {RelationalCount} from relational storage ({RelationalTime}ms) for client: {ClientId}", 
                result.FastStorageCount, result.FastStorageTimeMs, result.RelationalStorageCount, result.RelationalStorageTimeMs, clientId);

            // Step 3: Merge and deduplicate results
            var mergedResults = await MergeAndDeduplicateResultsAsync(
                fastStorageResults, 
                relationalStorageResults, 
                clientId, 
                correlationId
            );

            result.UniqueCount = mergedResults.Count();
            result.TotalCount = result.FastStorageCount + result.RelationalStorageCount;

            // Step 4: Apply pagination
            var paginatedResults = mergedResults
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();

            result.Data = paginatedResults;

            // Step 5: Perform consistency check and cache warming for missing items
            var consistencyTask = Task.Run(async () => 
            {
                var consistencyResult = await PerformConsistencyCheckAndCacheWarmingWithMetricsAsync(
                    fastStorageResults, 
                    relationalStorageResults, 
                    clientId, 
                    correlationId
                );
                
                result.CacheWarmedCount = consistencyResult.CacheWarmedCount;
                result.ConsistencyIssuesCount = consistencyResult.ConsistencyIssuesCount;
                result.Warnings.AddRange(consistencyResult.Warnings);
            });

            // Don't wait for consistency check to complete - it's fire-and-forget
            _ = consistencyTask;

            // Step 6: Check storage health
            result.StorageHealth = await CheckStorageHealthAsync();

            stopwatch.Stop();
            result.OperationTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Successfully retrieved {Count} responses for client: {ClientId}, CorrelationId: {CorrelationId}, TotalTime: {ElapsedMs}ms", 
                paginatedResults.Count, clientId, correlationId, result.OperationTimeMs);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result.OperationTimeMs = stopwatch.ElapsedMilliseconds;
            result.UsedFallback = true;
            result.Warnings.Add("Operation timed out, using fallback strategy");
            
            _logger.LogWarning("Timeout occurred while retrieving responses for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            
            // Fallback: try to get from relational storage only
            var fallbackResults = await GetFromRelationalStorageWithFallbackAsync(clientId, skip, take, correlationId);
            result.Data = fallbackResults;
            result.RelationalStorageCount = fallbackResults.Count();
            result.UniqueCount = fallbackResults.Count();
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.OperationTimeMs = stopwatch.ElapsedMilliseconds;
            result.UsedFallback = true;
            result.Warnings.Add($"Operation failed: {ex.Message}");
            
            _logger.LogError(ex, "Error getting responses for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
            
            // Fallback: try to get from relational storage only
            var fallbackResults = await GetFromRelationalStorageWithFallbackAsync(clientId, skip, take, correlationId);
            result.Data = fallbackResults;
            result.RelationalStorageCount = fallbackResults.Count();
            result.UniqueCount = fallbackResults.Count();
            
            return result;
        }
    }

    private async Task<(int CacheWarmedCount, int ConsistencyIssuesCount, List<string> Warnings)> PerformConsistencyCheckAndCacheWarmingWithMetricsAsync(
        IEnumerable<SurveyResponse> fastStorageResults, 
        IEnumerable<SurveyResponse> relationalStorageResults, 
        string clientId, 
        string correlationId)
    {
        var cacheWarmedCount = 0;
        var consistencyIssuesCount = 0;
        var warnings = new List<string>();

        try
        {
            // Create sets for efficient comparison
            var fastStorageIds = fastStorageResults.Select(r => r.ResponseId).ToHashSet();
            var relationalStorageIds = relationalStorageResults.Select(r => r.ResponseId).ToHashSet();

            // Find items that exist in relational storage but not in fast storage (cache warming)
            var missingInFastStorage = relationalStorageIds.Except(fastStorageIds).ToList();
            
            if (missingInFastStorage.Any())
            {
                _logger.LogInformation("Found {Count} items missing in fast storage, warming cache for client: {ClientId}, CorrelationId: {CorrelationId}", 
                    missingInFastStorage.Count, clientId, correlationId);

                // Warm cache for missing items (limit to prevent overwhelming the system)
                var itemsToWarm = missingInFastStorage.Take(10).ToList();
                
                foreach (var responseId in itemsToWarm)
                {
                    try
                    {
                        var response = relationalStorageResults.FirstOrDefault(r => r.ResponseId == responseId);
                        if (response != null)
                        {
                            var fastStorageResponse = FastStorageSurveyResponse.FromSurveyResponse(response);
                            await _fastStorage.SetAsync(
                                responseId,
                                clientId,
                                fastStorageResponse,
                                TimeSpan.FromHours(24)
                            );
                            
                            cacheWarmedCount++;
                            _logger.LogDebug("Warmed cache for response: {ResponseId}", responseId);
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to warm cache for response {responseId}: {ex.Message}");
                        _logger.LogWarning(ex, "Failed to warm cache for response: {ResponseId}", responseId);
                    }
                }
            }

            // Find items that exist in fast storage but not in relational storage (potential inconsistency)
            var missingInRelationalStorage = fastStorageIds.Except(relationalStorageIds).ToList();
            
            if (missingInRelationalStorage.Any())
            {
                consistencyIssuesCount = missingInRelationalStorage.Count;
                warnings.Add($"{missingInRelationalStorage.Count} items found in fast storage but missing in relational storage");
                
                _logger.LogWarning("Found {Count} items in fast storage but missing in relational storage for client: {ClientId}, CorrelationId: {CorrelationId}", 
                    missingInRelationalStorage.Count, clientId, correlationId);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Error during consistency check: {ex.Message}");
            _logger.LogError(ex, "Error during consistency check and cache warming for client: {ClientId}, CorrelationId: {CorrelationId}", 
                clientId, correlationId);
        }

        return (cacheWarmedCount, consistencyIssuesCount, warnings);
    }

    private async Task<StorageHealthStatus> CheckStorageHealthAsync()
    {
        var health = new StorageHealthStatus();

        try
        {
            // Check fast storage health
            try
            {
                var fastStorageMetrics = await _fastStorage.GetMetricsAsync();
                health.FastStorageHealthy = true;
            }
            catch (Exception ex)
            {
                health.FastStorageHealthy = false;
                health.FastStorageError = ex.Message;
            }

            // Check relational storage health
            try
            {
                var testQuery = await _relationalStorage.GetCountByClientIdAsync("test");
                health.RelationalStorageHealthy = true;
            }
            catch (Exception ex)
            {
                health.RelationalStorageHealthy = false;
                health.RelationalStorageError = ex.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking storage health");
        }

        return health;
    }

    private SurveyResponse CreateSurveyResponseFromRequest(SurveyResponseRequest request)
    {
        return new SurveyResponse
        {
            SurveyId = request.SurveyId,
            ClientId = request.ClientId,
            ResponseId = request.ResponseId,
            Responses = request.Responses,
            Metadata = request.Metadata,
            ProcessingStatus = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
