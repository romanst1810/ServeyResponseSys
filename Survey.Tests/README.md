# Survey Response System - Unit Tests

This directory contains comprehensive unit tests for the Survey Response System, covering all aspects of data access, business logic, and integration scenarios.

## Test Structure

### 1. **SurveyResponseRepositoryTests.cs** - Repository Layer Tests
Tests the data access layer using Entity Framework In-Memory Database.

**Coverage:**
- ✅ **CRUD Operations**: Create, Read, Update, Delete survey responses
- ✅ **Data Retrieval**: Get by ID, Client ID, Survey ID with pagination
- ✅ **Aggregations**: Count operations, NPS averages with date filtering
- ✅ **Data Mapping**: Entity to Model and Model to Entity conversions
- ✅ **Custom Fields**: JSON serialization/deserialization
- ✅ **Error Handling**: Exception scenarios and edge cases

**Key Test Methods:**
- `CreateAsync_ShouldCreateNewSurveyResponse()`
- `GetByIdAsync_ShouldReturnSurveyResponse_WhenExists()`
- `GetByClientIdAsync_ShouldReturnAllResponsesForClient()`
- `UpdateAsync_ShouldUpdateExistingSurveyResponse()`
- `DeleteAsync_ShouldDeleteExistingSurveyResponse()`
- `GetAverageNpsByClientIdAsync_ShouldReturnCorrectAverage()`
- `MapToModel_ShouldCorrectlyMapEntityToModel()`

### 2. **SurveyResponseServiceTests.cs** - Service Layer Tests
Tests the business logic layer using mocked dependencies.

**Coverage:**
- ✅ **Request Processing**: Survey response submission workflow
- ✅ **Metrics Calculation**: NPS metrics with caching logic
- ✅ **Service Integration**: Repository, Fast Storage, Queue, Metrics services
- ✅ **Error Handling**: Service failures and exception propagation
- ✅ **Caching Logic**: Metrics caching and retrieval
- ✅ **Processing Status**: Queue processing status management

**Key Test Methods:**
- `ProcessSurveyResponseAsync_ShouldProcessRequestSuccessfully()`
- `GetNpsMetricsAsync_ShouldReturnCachedMetrics_WhenAvailable()`
- `GetNpsMetricsAsync_ShouldCalculateAndCacheMetrics_WhenNotCached()`
- `GetResponsesByClientAsync_ShouldReturnClientResponses()`
- `GetProcessingStatusAsync_ShouldReturnProcessingStatus()`

### 3. **SurveyResponseIntegrationTests.cs** - Integration Tests
Tests the complete flow from data creation to retrieval using real database operations.

**Coverage:**
- ✅ **End-to-End Flow**: Complete data lifecycle
- ✅ **Multiple Operations**: Batch operations and data consistency
- ✅ **Pagination**: Large dataset handling
- ✅ **Date Filtering**: Time-based queries
- ✅ **Complex Scenarios**: Multiple clients, surveys, responses
- ✅ **Data Integrity**: Verify data persistence and retrieval

**Key Test Methods:**
- `FullIntegration_ShouldCreateAndRetrieveSurveyResponse()`
- `Integration_ShouldHandleMultipleResponsesForSameClient()`
- `Integration_ShouldCalculateCorrectNpsAverages()`
- `Integration_ShouldHandlePaginationCorrectly()`
- `Integration_ShouldHandleCustomFieldsSerialization()`

## Running the Tests

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

### Command Line
```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~SurveyResponseRepositoryTests"

# Run with verbose output
dotnet test --verbosity normal

# Run with coverage (if coverlet is installed)
dotnet test --collect:"XPlat Code Coverage"
```

### Visual Studio
1. Open the solution in Visual Studio
2. Open Test Explorer (Test > Test Explorer)
3. Click "Run All" or run individual tests

### VS Code
1. Install the .NET Core Test Explorer extension
2. Open the test file
3. Click the "Run Test" or "Debug Test" buttons above test methods

## Test Data

### Sample Survey Response
```csharp
var request = new SurveyResponseRequest
{
    SurveyId = "test_survey_001",
    ClientId = "test_client_001",
    ResponseId = "test_response_001",
    Responses = new SurveyResponses
    {
        NpsScore = 8,
        Satisfaction = "satisfied",
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
```

## Test Categories

### Unit Tests (Repository & Service)
- **Isolation**: Each test is independent
- **Speed**: Fast execution using in-memory database
- **Mocking**: External dependencies are mocked
- **Coverage**: 100% method coverage for business logic

### Integration Tests
- **Real Database**: Uses Entity Framework In-Memory
- **End-to-End**: Tests complete workflows
- **Data Persistence**: Verifies data is actually saved/retrieved
- **Complex Scenarios**: Multiple operations in sequence

## Assertions

Tests use **FluentAssertions** for readable assertions:

```csharp
// Simple assertions
result.Should().NotBeNull();
result.ResponseId.Should().Be(expectedId);

// Collection assertions
results.Should().HaveCount(3);
results.Should().OnlyContain(r => r.ClientId == clientId);

// Exception assertions
await service.Invoking(s => s.ProcessRequest(request))
    .Should().ThrowAsync<Exception>()
    .WithMessage("Expected error message");
```

## Mocking

Tests use **Moq** for mocking dependencies:

```csharp
var mockRepository = new Mock<ISurveyResponseRepository>();
mockRepository.Setup(x => x.GetByIdAsync(It.IsAny<string>()))
    .ReturnsAsync(expectedResponse);
```

## Database Testing

- **In-Memory Database**: Uses EF Core In-Memory provider
- **Isolation**: Each test gets a fresh database instance
- **No External Dependencies**: Tests run without SQL Server
- **Fast Execution**: No network calls or disk I/O

## Coverage Areas

### Repository Layer
- [x] Create operations
- [x] Read operations (single, multiple, filtered)
- [x] Update operations
- [x] Delete operations
- [x] Count and aggregation operations
- [x] Date range filtering
- [x] Pagination
- [x] Data mapping (Entity ↔ Model)
- [x] JSON serialization/deserialization
- [x] Error handling and exceptions

### Service Layer
- [x] Request processing workflow
- [x] Metrics calculation and caching
- [x] Service integration
- [x] Error propagation
- [x] Business logic validation
- [x] Processing status management

### Integration Layer
- [x] Complete data lifecycle
- [x] Multiple concurrent operations
- [x] Data consistency
- [x] Performance with large datasets
- [x] Complex business scenarios

## Best Practices

1. **Arrange-Act-Assert**: Clear test structure
2. **Descriptive Names**: Test names describe the scenario
3. **Single Responsibility**: Each test verifies one thing
4. **Independent Tests**: No test dependencies
5. **Realistic Data**: Test data resembles production data
6. **Edge Cases**: Test boundary conditions and error scenarios
7. **Performance**: Tests run quickly (< 1 second each)

## Troubleshooting

### Common Issues
1. **Test Not Found**: Ensure test project references are correct
2. **Mock Setup Errors**: Verify mock configurations match actual method signatures
3. **Database Errors**: Check Entity Framework configuration
4. **Async/Await**: Ensure proper async test method signatures

### Debugging
```bash
# Run tests with debug output
dotnet test --logger "console;verbosity=detailed"

# Run specific failing test
dotnet test --filter "FullyQualifiedName~SpecificTestName"
```

## Contributing

When adding new tests:
1. Follow the existing naming conventions
2. Use the provided helper methods for test data
3. Ensure tests are independent and repeatable
4. Add appropriate assertions for all scenarios
5. Update this README if adding new test categories
