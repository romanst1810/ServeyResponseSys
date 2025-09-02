using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Survey.Core.Models;
using Survey.Infrastructure.Data;
using Survey.Infrastructure.Repositories;
using Survey.Infrastructure.Data.Entities;

namespace Survey.Tests;

public class SurveyResponseRepositoryTests
{
    private readonly DbContextOptions<SurveyResponseDbContext> _options;
    private readonly ILogger<SurveyResponseRepository> _logger;

    public SurveyResponseRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<SurveyResponseDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _logger = Mock.Of<ILogger<SurveyResponseRepository>>();
    }

    private SurveyResponseDbContext CreateContext()
    {
        return new SurveyResponseDbContext(_options);
    }

    private SurveyResponseRepository CreateRepository(SurveyResponseDbContext context)
    {
        return new SurveyResponseRepository(context, _logger);
    }

    private Survey.Core.Models.SurveyResponse CreateTestSurveyResponse(
        string responseId = "test_response_001",
        string surveyId = "test_survey_001",
        string clientId = "test_client_001",
        int? npsScore = 8,
        string satisfaction = "satisfied")
    {
        return new Survey.Core.Models.SurveyResponse
        {
            ResponseId = responseId,
            SurveyId = surveyId,
            ClientId = clientId,
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
            },
            ProcessingStatus = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateNewSurveyResponse()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var surveyResponse = CreateTestSurveyResponse();

        // Act
        var result = await repository.CreateAsync(surveyResponse);

        // Assert
        result.Should().NotBeNull();
        result.ResponseId.Should().Be(surveyResponse.ResponseId);
        result.SurveyId.Should().Be(surveyResponse.SurveyId);
        result.ClientId.Should().Be(surveyResponse.ClientId);
        result.Responses.NpsScore.Should().Be(surveyResponse.Responses.NpsScore);
        result.Responses.Satisfaction.Should().Be(surveyResponse.Responses.Satisfaction);

        // Verify it was saved to database
        var savedEntity = await context.SurveyResponses.FirstOrDefaultAsync(e => e.ResponseId == surveyResponse.ResponseId);
        savedEntity.Should().NotBeNull();
        savedEntity!.ResponseId.Should().Be(surveyResponse.ResponseId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSurveyResponse_WhenExists()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var surveyResponse = CreateTestSurveyResponse();
        await repository.CreateAsync(surveyResponse);

        // Act
        var result = await repository.GetByIdAsync(surveyResponse.ResponseId);

        // Assert
        result.Should().NotBeNull();
        result!.ResponseId.Should().Be(surveyResponse.ResponseId);
        result.SurveyId.Should().Be(surveyResponse.SurveyId);
        result.ClientId.Should().Be(surveyResponse.ClientId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetByIdAsync("non_existent_id");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByClientIdAsync_ShouldReturnAllResponsesForClient()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var clientId = "test_client_001";
        var response1 = CreateTestSurveyResponse("response_001", "survey_001", clientId);
        var response2 = CreateTestSurveyResponse("response_002", "survey_001", clientId);
        var response3 = CreateTestSurveyResponse("response_003", "survey_002", "different_client");

        await repository.CreateAsync(response1);
        await repository.CreateAsync(response2);
        await repository.CreateAsync(response3);

        // Act
        var results = await repository.GetByClientIdAsync(clientId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.ClientId == clientId);
    }

    [Fact]
    public async Task GetByClientIdAsync_ShouldRespectSkipAndTake()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var clientId = "test_client_001";
        for (int i = 1; i <= 5; i++)
        {
            var response = CreateTestSurveyResponse($"response_{i:D3}", "survey_001", clientId);
            await repository.CreateAsync(response);
        }

        // Act
        var results = await repository.GetByClientIdAsync(clientId, skip: 2, take: 2);

        // Assert
        results.Should().HaveCount(2);
       
        var resultList = results.ToList();
        resultList[0].ResponseId.Should().Be("response_003");
        resultList[1].ResponseId.Should().Be("response_002");
    }

    [Fact]
    public async Task GetBySurveyIdAsync_ShouldReturnAllResponsesForSurvey()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var surveyId = "test_survey_001";
        var response1 = CreateTestSurveyResponse("response_001", surveyId, "client_001");
        var response2 = CreateTestSurveyResponse("response_002", surveyId, "client_002");
        var response3 = CreateTestSurveyResponse("response_003", "different_survey", "client_001");

        await repository.CreateAsync(response1);
        await repository.CreateAsync(response2);
        await repository.CreateAsync(response3);

        // Act
        var results = await repository.GetBySurveyIdAsync(surveyId);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.SurveyId == surveyId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExistingSurveyResponse()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var surveyResponse = CreateTestSurveyResponse();
        await repository.CreateAsync(surveyResponse);

        // Update the response
        surveyResponse.Responses.NpsScore = 10;
        surveyResponse.Responses.Satisfaction = "very_satisfied";
        surveyResponse.ProcessingStatus = "completed";

        // Act
        var result = await repository.UpdateAsync(surveyResponse);

        // Assert
        result.Should().NotBeNull();
        result.Responses.NpsScore.Should().Be(10);
        result.Responses.Satisfaction.Should().Be("very_satisfied");
        result.ProcessingStatus.Should().Be("completed");

        // Verify it was updated in database
        var updatedEntity = await context.SurveyResponses.FirstOrDefaultAsync(e => e.ResponseId == surveyResponse.ResponseId);
        updatedEntity.Should().NotBeNull();
        updatedEntity!.NpsScore.Should().Be(10);
        updatedEntity.Satisfaction.Should().Be("very_satisfied");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenResponseNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var surveyResponse = CreateTestSurveyResponse();

        // Act & Assert
        await repository.Invoking(r => r.UpdateAsync(surveyResponse))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Survey response not found: {surveyResponse.ResponseId}");
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteExistingSurveyResponse()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var surveyResponse = CreateTestSurveyResponse();
        await repository.CreateAsync(surveyResponse);

        // Act
        var result = await repository.DeleteAsync(surveyResponse.ResponseId);

        // Assert
        result.Should().BeTrue();

        // Verify it was deleted from database
        var deletedEntity = await context.SurveyResponses.FirstOrDefaultAsync(e => e.ResponseId == surveyResponse.ResponseId);
        deletedEntity.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenResponseNotFound()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.DeleteAsync("non_existent_id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetCountByClientIdAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var clientId = "test_client_001";
        for (int i = 1; i <= 3; i++)
        {
            var response = CreateTestSurveyResponse($"response_{i:D3}", "survey_001", clientId);
            await repository.CreateAsync(response);
        }

        // Create one response for different client
        var otherResponse = CreateTestSurveyResponse("response_004", "survey_001", "different_client");
        await repository.CreateAsync(otherResponse);

        // Act
        var count = await repository.GetCountByClientIdAsync(clientId);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetCountBySurveyIdAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var surveyId = "test_survey_001";
        for (int i = 1; i <= 4; i++)
        {
            var response = CreateTestSurveyResponse($"response_{i:D3}", surveyId, $"client_{i:D3}");
            await repository.CreateAsync(response);
        }

        // Create one response for different survey
        var otherResponse = CreateTestSurveyResponse("response_005", "different_survey", "client_005");
        await repository.CreateAsync(otherResponse);

        // Act
        var count = await repository.GetCountBySurveyIdAsync(surveyId);

        // Assert
        count.Should().Be(4);
    }

    [Fact]
    public async Task GetAverageNpsByClientIdAsync_ShouldReturnCorrectAverage()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var clientId = "test_client_001";
        var responses = new[]
        {
            CreateTestSurveyResponse("response_001", "survey_001", clientId, 8),
            CreateTestSurveyResponse("response_002", "survey_001", clientId, 9),
            CreateTestSurveyResponse("response_003", "survey_001", clientId, 7),
            CreateTestSurveyResponse("response_004", "survey_001", clientId, null) // No NPS score
        };

        foreach (var response in responses)
        {
            await repository.CreateAsync(response);
        }

        // Act
        var average = await repository.GetAverageNpsByClientIdAsync(clientId);

        // Assert
        average.Should().Be(8.0m); // (8 + 9 + 7) / 3 = 8.0
    }

    [Fact]
    public async Task GetAverageNpsByClientIdAsync_ShouldRespectDateRange()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var clientId = "test_client_001";
        var baseDate = DateTime.UtcNow.Date;
        
        var responses = new[]
        {
            CreateTestSurveyResponse("response_001", "survey_001", clientId, 8),
            CreateTestSurveyResponse("response_002", "survey_001", clientId, 9),
            CreateTestSurveyResponse("response_003", "survey_001", clientId, 7)
        };

        // Set different creation dates
        responses[0].CreatedAt = baseDate.AddDays(-5);
        responses[1].CreatedAt = baseDate.AddDays(-3);
        responses[2].CreatedAt = baseDate.AddDays(-1);

        foreach (var response in responses)
        {
            await repository.CreateAsync(response);
        }

        // Act
        var average = await repository.GetAverageNpsByClientIdAsync(clientId, 
            fromDate: baseDate.AddDays(-4), 
            toDate: baseDate.AddDays(-2));

        // Assert
        average.Should().Be(9.0m); // Only response_002 (9) is in range
    }

    [Fact]
    public async Task GetAverageNpsBySurveyIdAsync_ShouldReturnCorrectAverage()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        
        var surveyId = "test_survey_001";
        var responses = new[]
        {
            CreateTestSurveyResponse("response_001", surveyId, "client_001", 10),
            CreateTestSurveyResponse("response_002", surveyId, "client_002", 8),
            CreateTestSurveyResponse("response_003", surveyId, "client_003", 6),
            CreateTestSurveyResponse("response_004", "different_survey", "client_004", 9) // Different survey
        };

        foreach (var response in responses)
        {
            await repository.CreateAsync(response);
        }

        // Act
        var average = await repository.GetAverageNpsBySurveyIdAsync(surveyId);

        // Assert
        average.Should().Be(8.0m); // (10 + 8 + 6) / 3 = 8.0
    }

    [Fact]
    public async Task MapToModel_ShouldCorrectlyMapEntityToModel()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var surveyResponse = CreateTestSurveyResponse();
        await repository.CreateAsync(surveyResponse);

        // Act
        var result = await repository.GetByIdAsync(surveyResponse.ResponseId);

        // Assert
        result.Should().NotBeNull();
        result!.Responses.CustomFields.Should().ContainKey("product_used");
        result.Responses.CustomFields["product_used"].ToString().Should().Be("mobile_app");
        result.Responses.CustomFields.Should().ContainKey("feature_rating");
        result.Responses.CustomFields["feature_rating"].ToString().Should().Be("9");
        result.Metadata.UserAgent.Should().Be("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        result.Metadata.IpAddress.Should().Be("192.168.1.100");
        result.Metadata.DeviceType.Should().Be("mobile");
    }

    [Fact]
    public async Task CreateAsync_ShouldHandleCustomFieldsSerialization()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var surveyResponse = CreateTestSurveyResponse();
        
        // Add complex custom fields
        surveyResponse.Responses.CustomFields = new Dictionary<string, object>
        {
            ["string_value"] = "test",
            ["int_value"] = 42,
            ["bool_value"] = true,
            ["array_value"] = new[] { 1, 2, 3 },
            ["nested_object"] = new Dictionary<string, object>
            {
                ["nested_key"] = "nested_value"
            }
        };

        // Act
        var result = await repository.CreateAsync(surveyResponse);

        // Assert
        result.Should().NotBeNull();
        result.Responses.CustomFields.Should().ContainKey("string_value");
        result.Responses.CustomFields.Should().ContainKey("int_value");
        result.Responses.CustomFields.Should().ContainKey("bool_value");
        result.Responses.CustomFields.Should().ContainKey("array_value");
        result.Responses.CustomFields.Should().ContainKey("nested_object");
    }
}
