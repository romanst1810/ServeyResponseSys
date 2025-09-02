using Microsoft.Extensions.Logging;
using Survey.Core.Interfaces;
using Survey.Core.Models;
using Survey.Infrastructure.Services;

namespace Survey.Tests;

public class SurveyResponseServiceTests
{
    private readonly Mock<IStorageOrchestrator> _mockStorageOrchestrator;
    private readonly Mock<IMetricsService> _mockMetricsService;
    private readonly Mock<IValidationService> _mockValidationService;
    private readonly Mock<ILogger<SurveyResponseService>> _mockLogger;
    private readonly SurveyResponseService _service;

    public SurveyResponseServiceTests()
    {
        _mockStorageOrchestrator = new Mock<IStorageOrchestrator>();
        _mockMetricsService = new Mock<IMetricsService>();
        _mockValidationService = new Mock<IValidationService>();
        _mockLogger = new Mock<ILogger<SurveyResponseService>>();

        _service = new SurveyResponseService(
            _mockStorageOrchestrator.Object,
            _mockMetricsService.Object,
            _mockValidationService.Object,
            _mockLogger.Object);
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
    public async Task ProcessSurveyResponseAsync_ShouldProcessRequestSuccessfully()
    {
        // Arrange
        var request = CreateTestRequest();
        var processingId = "processing_123";
        var expectedResult = new SurveyResponseResult
        {
            ResponseId = request.ResponseId,
            Status = "accepted",
            ProcessingId = processingId,
            EstimatedProcessingTime = "30 seconds",
            FastStorageStatus = "available",
            RelationalStorageStatus = "queued",
            ProcessingTime = 100,
            CreatedAt = DateTime.UtcNow
        };
        
        _mockValidationService.Setup(x => x.ValidateSurveyResponseAsync(request))
            .ReturnsAsync(new ValidationResult { IsValid = true });
        
        _mockStorageOrchestrator.Setup(x => x.ProcessSurveyResponseAsync(request))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.ProcessSurveyResponseAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.ResponseId.Should().Be(request.ResponseId);
        result.Status.Should().Be("accepted");
        result.ProcessingId.Should().Be(processingId);
        result.EstimatedProcessingTime.Should().Be("30 seconds");

        _mockValidationService.Verify(x => x.ValidateSurveyResponseAsync(request), Times.Once);
        _mockStorageOrchestrator.Verify(x => x.ProcessSurveyResponseAsync(request), Times.Once);
    }

    [Fact]
    public async Task ProcessSurveyResponseAsync_ShouldHandleValidationFailure()
    {
        // Arrange
        var request = CreateTestRequest();
        var validationResult = new ValidationResult 
        { 
            IsValid = false, 
            Errors = new List<ValidationError> { new ValidationError { Field = "NpsScore", Message = "Invalid NPS score" } }
        };
        
        _mockValidationService.Setup(x => x.ValidateSurveyResponseAsync(request))
            .ReturnsAsync(validationResult);

        // Act & Assert
        await _service.Invoking(s => s.ProcessSurveyResponseAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*validation failed*");

        _mockStorageOrchestrator.Verify(x => x.ProcessSurveyResponseAsync(request), Times.Never);
    }

    [Fact]
    public async Task ProcessSurveyResponseAsync_ShouldHandleStorageOrchestratorFailure()
    {
        // Arrange
        var request = CreateTestRequest();
        var expectedException = new Exception("Storage orchestrator error");
        
        _mockValidationService.Setup(x => x.ValidateSurveyResponseAsync(request))
            .ReturnsAsync(new ValidationResult { IsValid = true });
        
        _mockStorageOrchestrator.Setup(x => x.ProcessSurveyResponseAsync(request))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await _service.Invoking(s => s.ProcessSurveyResponseAsync(request))
            .Should().ThrowAsync<Exception>()
            .WithMessage("Storage orchestrator error");
    }

    [Fact]
    public async Task GetNpsMetricsAsync_ShouldReturnMetrics_WhenAvailable()
    {
        // Arrange
        var clientId = "test_client_001";
        var surveyId = "test_survey_001";
        var expectedMetrics = new NpsMetrics
        {
            ClientId = clientId,
            SurveyId = surveyId,
            Period = "day",
            Metrics = new NpsMetricsData
            {
                CurrentNps = 8.5m,
                TotalResponses = 100,
                ResponseCount = 50
            }
        };

        _mockMetricsService.Setup(x => x.CalculateNpsMetricsAsync(clientId, surveyId, "day"))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _service.GetNpsMetricsAsync(clientId, surveyId);

        // Assert
        result.Should().NotBeNull();
        result.ClientId.Should().Be(clientId);
        result.SurveyId.Should().Be(surveyId);
        result.Metrics.CurrentNps.Should().Be(8.5m);

        _mockMetricsService.Verify(x => x.CalculateNpsMetricsAsync(clientId, surveyId, "day"), Times.Once);
    }

    [Fact]
    public async Task GetNpsMetricsAsync_ShouldHandleMetricsServiceFailure()
    {
        // Arrange
        var clientId = "test_client_001";
        var surveyId = "test_survey_001";
        var expectedException = new Exception("Metrics service error");

        _mockMetricsService.Setup(x => x.CalculateNpsMetricsAsync(clientId, surveyId, "day"))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await _service.Invoking(s => s.GetNpsMetricsAsync(clientId, surveyId))
            .Should().ThrowAsync<Exception>()
            .WithMessage("Metrics service error");
    }

    [Fact]
    public async Task GetNpsMetricsAsync_ShouldUseDefaultPeriod_WhenNotSpecified()
    {
        // Arrange
        var clientId = "test_client_001";
        var expectedMetrics = new NpsMetrics
        {
            ClientId = clientId,
            Period = "day",
            Metrics = new NpsMetricsData
            {
                CurrentNps = 8.0m,
                TotalResponses = 50,
                ResponseCount = 30
            }
        };

        _mockMetricsService.Setup(x => x.CalculateNpsMetricsAsync(clientId, null, "day"))
            .ReturnsAsync(expectedMetrics);

        // Act
        var result = await _service.GetNpsMetricsAsync(clientId);

        // Assert
        result.Should().NotBeNull();
        result.Period.Should().Be("day");

        _mockMetricsService.Verify(x => x.CalculateNpsMetricsAsync(clientId, null, "day"), Times.Once);
    }



    [Fact]
    public async Task GetResponsesByClientAsync_ShouldReturnClientResponses()
    {
        // Arrange
        var clientId = "test_client_001";
        var skip = 0;
        var take = 10;
        var expectedResponses = new List<Survey.Core.Models.SurveyResponse>
        {
            new Survey.Core.Models.SurveyResponse
            {
                ResponseId = "response_001",
                ClientId = clientId,
                SurveyId = "survey_001",
                Responses = new SurveyResponses { NpsScore = 8 },
                CreatedAt = DateTime.UtcNow
            },
            new Survey.Core.Models.SurveyResponse
            {
                ResponseId = "response_002",
                ClientId = clientId,
                SurveyId = "survey_001",
                Responses = new SurveyResponses { NpsScore = 9 },
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockStorageOrchestrator.Setup(x => x.GetResponsesByClientAsync(clientId, skip, take))
            .ReturnsAsync(expectedResponses);

        // Act
        var result = await _service.GetResponsesByClientAsync(clientId, skip, take);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.ClientId == clientId);

        _mockStorageOrchestrator.Verify(x => x.GetResponsesByClientAsync(clientId, skip, take), Times.Once);
    }

    [Fact]
    public async Task GetResponsesByClientAsync_ShouldUseDefaultPagination_WhenNotSpecified()
    {
        // Arrange
        var clientId = "test_client_001";
        var expectedResponses = new List<Survey.Core.Models.SurveyResponse>();

        _mockStorageOrchestrator.Setup(x => x.GetResponsesByClientAsync(clientId, 0, 100))
            .ReturnsAsync(expectedResponses);

        // Act
        var result = await _service.GetResponsesByClientAsync(clientId);

        // Assert
        result.Should().NotBeNull();

        _mockStorageOrchestrator.Verify(x => x.GetResponsesByClientAsync(clientId, 0, 100), Times.Once);
    }

    [Fact]
    public async Task GetProcessingStatusAsync_ShouldReturnProcessingStatus()
    {
        // Arrange
        var processingId = "processing_123";

        // Act
        var result = await _service.GetProcessingStatusAsync(processingId);

        // Assert
        result.Should().NotBeNull();
        result.ProcessingId.Should().Be(processingId);
        result.Status.Should().Be("completed");
        result.Progress.Should().Be(100);
        result.Stages.Should().HaveCount(3);
        result.Result.Should().NotBeNull();
        result.Result!.StoredInFastStorage.Should().BeTrue();
        result.Result.StoredInRelationalDb.Should().BeTrue();
        result.Result.MetricsUpdated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSurveyResponseAsync_ShouldReturnTrue_ForValidRequest()
    {
        // Arrange
        var request = CreateTestRequest();
        _mockValidationService.Setup(x => x.ValidateSurveyResponseAsync(request))
            .ReturnsAsync(new ValidationResult { IsValid = true });

        // Act
        var result = await _service.ValidateSurveyResponseAsync(request);

        // Assert
        result.Should().BeTrue();
        _mockValidationService.Verify(x => x.ValidateSurveyResponseAsync(request), Times.Once);
    }

    [Fact]
    public async Task ValidateSurveyResponseAsync_ShouldReturnFalse_ForInvalidRequest()
    {
        // Arrange
        var request = CreateTestRequest();
        request.ResponseId = ""; // Invalid - empty response ID
        _mockValidationService.Setup(x => x.ValidateSurveyResponseAsync(request))
            .ReturnsAsync(new ValidationResult { IsValid = false });

        // Act
        var result = await _service.ValidateSurveyResponseAsync(request);

        // Assert
        result.Should().BeFalse();
        _mockValidationService.Verify(x => x.ValidateSurveyResponseAsync(request), Times.Once);
    }

    [Fact]
    public async Task ValidateSurveyResponseAsync_ShouldReturnFalse_ForInvalidNpsScore()
    {
        // Arrange
        var request = CreateTestRequest();
        request.Responses.NpsScore = 15; // Invalid - NPS score should be 0-10
        _mockValidationService.Setup(x => x.ValidateSurveyResponseAsync(request))
            .ReturnsAsync(new ValidationResult { IsValid = false });

        // Act
        var result = await _service.ValidateSurveyResponseAsync(request);

        // Assert
        result.Should().BeFalse();
        _mockValidationService.Verify(x => x.ValidateSurveyResponseAsync(request), Times.Once);
    }



    [Fact]
    public async Task GetNpsMetricsAsync_ShouldHandleNullSurveyId()
    {
        // Arrange
        var clientId = "test_client_001";
        var calculatedMetrics = new NpsMetrics
        {
            ClientId = clientId,
            SurveyId = null,
            Period = "week",
            Metrics = new NpsMetricsData
            {
                CurrentNps = 8.2m,
                TotalResponses = 200,
                ResponseCount = 150
            }
        };

        _mockMetricsService.Setup(x => x.CalculateNpsMetricsAsync(clientId, null, "week"))
            .ReturnsAsync(calculatedMetrics);

        // Act
        var result = await _service.GetNpsMetricsAsync(clientId, null, "week");

        // Assert
        result.Should().NotBeNull();
        result.ClientId.Should().Be(clientId);
        result.SurveyId.Should().BeNull();
        result.Period.Should().Be("week");

        _mockMetricsService.Verify(x => x.CalculateNpsMetricsAsync(clientId, null, "week"), Times.Once);
    }
}
