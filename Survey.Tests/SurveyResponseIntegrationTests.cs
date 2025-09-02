using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using Survey.Infrastructure.Data;
using Survey.Infrastructure.Repositories;
using Survey.Infrastructure.Services;

namespace Survey.Tests;

public class SurveyResponseIntegrationTests
{
    private readonly DbContextOptions<SurveyResponseDbContext> _options;
    private readonly SurveyResponseDbContext _context;
    private readonly ISurveyResponseRepository _repository;
    private readonly ISurveyResponseService _service;

    public SurveyResponseIntegrationTests()
    {
        _options = new DbContextOptionsBuilder<SurveyResponseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new SurveyResponseDbContext(_options);
        _repository = new SurveyResponseRepository(_context, Mock.Of<ILogger<SurveyResponseRepository>>());
        
        var mockStorageOrchestrator = new Mock<IStorageOrchestrator>();
        var mockMetricsService = new Mock<IMetricsService>();
        var mockValidationService = new Mock<IValidationService>();
        var mockLogger = Mock.Of<ILogger<SurveyResponseService>>();
        
        _service = new SurveyResponseService(
            mockStorageOrchestrator.Object,
            mockMetricsService.Object,
            mockValidationService.Object,
            mockLogger);
    }

    private void Dispose()
    {
        _context?.Dispose();
    }

    private SurveyResponseRequest CreateTestRequest(
        string responseId = "test_response_001",
        string surveyId = "test_survey_001",
        string clientId = "test_client_001",
        int? npsScore = 8,
        string satisfaction = "satisfied")
    {
        return new SurveyResponseRequest
        {
            SurveyId = surveyId,
            ClientId = clientId,
            ResponseId = responseId,
            Responses = new SurveyResponses
            {
                NpsScore = npsScore,
                Satisfaction = satisfaction,
                CustomFields = new Dictionary<string, object>
                {
                    ["product_used"] = "mobile_app",
                    ["feature_rating"] = 9
                }
            },
            Metadata = new ResponseMetadata
            {
                Timestamp = DateTime.UtcNow,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
                IpAddress = "192.168.1.100",
                SessionId = "session_123",
                DeviceType = "mobile"
            }
        };
    }

    [Fact]
    public async Task FullIntegration_ShouldCreateAndRetrieveSurveyResponse()
    {
        // Arrange
        var request = CreateTestRequest();

        // Act - Create the survey response
        var createResult = await _repository.CreateAsync(new Survey.Core.Models.SurveyResponse
        {
            ResponseId = request.ResponseId,
            SurveyId = request.SurveyId,
            ClientId = request.ClientId,
            Responses = request.Responses,
            Metadata = request.Metadata,
            ProcessingStatus = "completed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Assert - Verify it was created
        createResult.Should().NotBeNull();
        createResult.ResponseId.Should().Be(request.ResponseId);

        // Act - Retrieve the survey response
        var retrieveResult = await _repository.GetByIdAsync(request.ResponseId);

        // Assert - Verify it can be retrieved
        retrieveResult.Should().NotBeNull();
        retrieveResult!.ResponseId.Should().Be(request.ResponseId);
        retrieveResult.SurveyId.Should().Be(request.SurveyId);
        retrieveResult.ClientId.Should().Be(request.ClientId);
        retrieveResult.Responses.NpsScore.Should().Be(request.Responses.NpsScore);
        retrieveResult.Responses.Satisfaction.Should().Be(request.Responses.Satisfaction);
    }

    [Fact]
    public async Task Integration_ShouldHandleMultipleResponsesForSameClient()
    {
        // Arrange
        var clientId = "test_client_001";
        var responses = new[]
        {
            CreateTestRequest("response_001", "survey_001", clientId, 8),
            CreateTestRequest("response_002", "survey_001", clientId, 9),
            CreateTestRequest("response_003", "survey_002", clientId, 7)
        };

        // Act - Create multiple responses
        foreach (var request in responses)
        {
            await _repository.CreateAsync(new Survey.Core.Models.SurveyResponse
            {
                ResponseId = request.ResponseId,
                SurveyId = request.SurveyId,
                ClientId = request.ClientId,
                Responses = request.Responses,
                Metadata = request.Metadata,
                ProcessingStatus = "completed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Act - Retrieve all responses for the client
        var clientResponses = await _repository.GetByClientIdAsync(clientId);

        // Assert
        clientResponses.Should().HaveCount(3);
        clientResponses.Should().OnlyContain(r => r.ClientId == clientId);
        clientResponses.Should().Contain(r => r.ResponseId == "response_001");
        clientResponses.Should().Contain(r => r.ResponseId == "response_002");
        clientResponses.Should().Contain(r => r.ResponseId == "response_003");
    }

    [Fact]
    public async Task Integration_ShouldCalculateCorrectNpsAverages()
    {
        // Arrange
        var clientId = "test_client_001";
        var responses = new[]
        {
            CreateTestRequest("response_001", "survey_001", clientId, 10),
            CreateTestRequest("response_002", "survey_001", clientId, 8),
            CreateTestRequest("response_003", "survey_001", clientId, 6),
            CreateTestRequest("response_004", "survey_001", clientId, null), // No NPS score
            CreateTestRequest("response_005", "survey_002", "different_client", 9) // Different client
        };

        // Act - Create responses
        foreach (var request in responses)
        {
            await _repository.CreateAsync(new Survey.Core.Models.SurveyResponse
            {
                ResponseId = request.ResponseId,
                SurveyId = request.SurveyId,
                ClientId = request.ClientId,
                Responses = request.Responses,
                Metadata = request.Metadata,
                ProcessingStatus = "completed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Act - Calculate averages
        var clientAverage = await _repository.GetAverageNpsByClientIdAsync(clientId);
        var surveyAverage = await _repository.GetAverageNpsBySurveyIdAsync("survey_001");

        // Assert
        clientAverage.Should().Be(8.0m); // (10 + 8 + 6) / 3 = 8.0
        surveyAverage.Should().Be(8.0m); // (10 + 8 + 6) / 3 = 8.0 (excluding different client)
    }

    [Fact]
    public async Task Integration_ShouldHandleUpdateOperations()
    {
        // Arrange
        var request = CreateTestRequest();
        var surveyResponse = new Survey.Core.Models.SurveyResponse
        {
            ResponseId = request.ResponseId,
            SurveyId = request.SurveyId,
            ClientId = request.ClientId,
            Responses = request.Responses,
            Metadata = request.Metadata,
            ProcessingStatus = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(surveyResponse);

        // Act - Update the response
        surveyResponse.Responses.NpsScore = 10;
        surveyResponse.Responses.Satisfaction = "very_satisfied";
        surveyResponse.ProcessingStatus = "completed";

        var updateResult = await _repository.UpdateAsync(surveyResponse);

        // Assert
        updateResult.Should().NotBeNull();
        updateResult.Responses.NpsScore.Should().Be(10);
        updateResult.Responses.Satisfaction.Should().Be("very_satisfied");
        updateResult.ProcessingStatus.Should().Be("completed");

        // Verify the update persisted
        var retrieved = await _repository.GetByIdAsync(request.ResponseId);
        retrieved.Should().NotBeNull();
        retrieved!.Responses.NpsScore.Should().Be(10);
        retrieved.Responses.Satisfaction.Should().Be("very_satisfied");
    }

    [Fact]
    public async Task Integration_ShouldHandleDeleteOperations()
    {
        // Arrange
        var request = CreateTestRequest();
        var surveyResponse = new Survey.Core.Models.SurveyResponse
        {
            ResponseId = request.ResponseId,
            SurveyId = request.SurveyId,
            ClientId = request.ClientId,
            Responses = request.Responses,
            Metadata = request.Metadata,
            ProcessingStatus = "completed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(surveyResponse);

        // Verify it exists
        var beforeDelete = await _repository.GetByIdAsync(request.ResponseId);
        beforeDelete.Should().NotBeNull();

        // Act - Delete the response
        var deleteResult = await _repository.DeleteAsync(request.ResponseId);

        // Assert
        deleteResult.Should().BeTrue();

        // Verify it was deleted
        var afterDelete = await _repository.GetByIdAsync(request.ResponseId);
        afterDelete.Should().BeNull();
    }

    [Fact]
    public async Task Integration_ShouldHandlePaginationCorrectly()
    {
        // Arrange
        var clientId = "test_client_001";
        for (int i = 1; i <= 10; i++)
        {
            var request = CreateTestRequest($"response_{i:D3}", "survey_001", clientId, i);
            await _repository.CreateAsync(new Survey.Core.Models.SurveyResponse
            {
                ResponseId = request.ResponseId,
                SurveyId = request.SurveyId,
                ClientId = request.ClientId,
                Responses = request.Responses,
                Metadata = request.Metadata,
                ProcessingStatus = "completed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Act - Test pagination
        var page1 = await _repository.GetByClientIdAsync(clientId, skip: 0, take: 3);
        var page2 = await _repository.GetByClientIdAsync(clientId, skip: 3, take: 3);
        var page3 = await _repository.GetByClientIdAsync(clientId, skip: 6, take: 3);

        // Assert
        page1.Should().HaveCount(3);
        page2.Should().HaveCount(3);
        page3.Should().HaveCount(3);

        // Verify no overlap between pages
        var allIds = page1.Select(r => r.ResponseId)
            .Concat(page2.Select(r => r.ResponseId))
            .Concat(page3.Select(r => r.ResponseId))
            .ToList();
        
        allIds.Should().HaveCount(9);
        allIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Integration_ShouldHandleCustomFieldsSerialization()
    {
        // Arrange
        var request = CreateTestRequest();
        request.Responses.CustomFields = new Dictionary<string, object>
        {
            ["string_value"] = "test_string",
            ["int_value"] = 42,
            ["bool_value"] = true,
            ["array_value"] = new[] { 1, 2, 3 },
            ["nested_object"] = new Dictionary<string, object>
            {
                ["nested_key"] = "nested_value",
                ["nested_number"] = 123
            }
        };

        var surveyResponse = new Survey.Core.Models.SurveyResponse
        {
            ResponseId = request.ResponseId,
            SurveyId = request.SurveyId,
            ClientId = request.ClientId,
            Responses = request.Responses,
            Metadata = request.Metadata,
            ProcessingStatus = "completed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await _repository.CreateAsync(surveyResponse);
        var retrieved = await _repository.GetByIdAsync(request.ResponseId);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Responses.CustomFields.Should().ContainKey("string_value");
        retrieved.Responses.CustomFields["string_value"].ToString().Should().Be("test_string");
        retrieved.Responses.CustomFields.Should().ContainKey("int_value");
        retrieved.Responses.CustomFields["int_value"].ToString().Should().Be("42");
        retrieved.Responses.CustomFields.Should().ContainKey("bool_value");
        retrieved.Responses.CustomFields["bool_value"].ToString().Should().Be("True");
        retrieved.Responses.CustomFields.Should().ContainKey("array_value");
        retrieved.Responses.CustomFields.Should().ContainKey("nested_object");
    }

    [Fact]
    public async Task Integration_ShouldHandleDateRangeFiltering()
    {
        // Arrange
        var clientId = "test_client_001";
        var baseDate = DateTime.UtcNow.Date;
        
        var responses = new[]
        {
            CreateTestRequest("response_001", "survey_001", clientId, 8),
            CreateTestRequest("response_002", "survey_001", clientId, 9),
            CreateTestRequest("response_003", "survey_001", clientId, 7)
        };

        // Set different creation dates
        for (int i = 0; i < responses.Length; i++)
        {
            var response = new Survey.Core.Models.SurveyResponse
            {
                ResponseId = responses[i].ResponseId,
                SurveyId = responses[i].SurveyId,
                ClientId = responses[i].ClientId,
                Responses = responses[i].Responses,
                Metadata = responses[i].Metadata,
                ProcessingStatus = "completed",
                CreatedAt = baseDate.AddDays(-(i + 1)),
                UpdatedAt = baseDate.AddDays(-(i + 1))
            };
            await _repository.CreateAsync(response);
        }

        // Act - Test date range filtering
        var fromDate = baseDate.AddDays(-3);
        var toDate = baseDate.AddDays(-1);
        var average = await _repository.GetAverageNpsByClientIdAsync(clientId, fromDate, toDate);

        // Assert
        average.Should().Be(8.0m); // Only response_002 (9) and response_003 (7) are in range: (9 + 7) / 2 = 8.0
    }

    [Fact]
    public async Task Integration_ShouldHandleCountOperations()
    {
        // Arrange
        var clientId = "test_client_001";
        var surveyId = "test_survey_001";
        
        for (int i = 1; i <= 5; i++)
        {
            var request = CreateTestRequest($"response_{i:D3}", surveyId, clientId, i);
            await _repository.CreateAsync(new Survey.Core.Models.SurveyResponse
            {
                ResponseId = request.ResponseId,
                SurveyId = request.SurveyId,
                ClientId = request.ClientId,
                Responses = request.Responses,
                Metadata = request.Metadata,
                ProcessingStatus = "completed",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Create one response for different client/survey
        var otherRequest = CreateTestRequest("response_006", "different_survey", "different_client", 10);
        await _repository.CreateAsync(new Survey.Core.Models.SurveyResponse
        {
            ResponseId = otherRequest.ResponseId,
            SurveyId = otherRequest.SurveyId,
            ClientId = otherRequest.ClientId,
            Responses = otherRequest.Responses,
            Metadata = otherRequest.Metadata,
            ProcessingStatus = "completed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Act
        var clientCount = await _repository.GetCountByClientIdAsync(clientId);
        var surveyCount = await _repository.GetCountBySurveyIdAsync(surveyId);

        // Assert
        clientCount.Should().Be(5);
        surveyCount.Should().Be(5);
    }
}
