using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Survey.Core.Interfaces;
using Survey.Infrastructure.Data;
using Survey.Infrastructure.Repositories;
using Survey.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/survey-response-api-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Entity Framework
builder.Services.AddDbContext<SurveyResponseDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        // Fallback to in-memory database if no connection string is provided
        options.UseInMemoryDatabase("SurveyResponseDb");
    }
});

// Register repositories
builder.Services.AddScoped<ISurveyResponseRepository, SurveyResponseRepository>();

// Register services
builder.Services.AddScoped<ISurveyResponseService, SurveyResponseService>();
builder.Services.AddScoped<IQueueService, InMemoryQueueService>();
builder.Services.AddScoped<IFastStorageRepository, InMemoryFastStorageService>();

// Register new dual storage services
builder.Services.AddScoped<IFastStorageService, DynamoDbSimulationService>();
builder.Services.AddScoped<IStorageOrchestrator, StorageOrchestrator>();

// Register background services
builder.Services.AddHostedService<DualStorageSyncService>();

builder.Services.AddScoped<IMetricsService, MetricsService>();
builder.Services.AddScoped<IValidationService, ValidationService>();

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2;
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

// Seed test data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SurveyResponseDbContext>();
    await context.Database.EnsureCreatedAsync();
    await SeedTestDataAsync(context);
}

app.Run();

static async Task SeedTestDataAsync(SurveyResponseDbContext context)
{
    if (context.SurveyResponses.Any())
    {
        return; // Data already seeded
    }

    // Seed clients
    var client = new Survey.Infrastructure.Data.Entities.ClientEntity
    {
        ClientId = "client_acme_corp",
        Name = "ACME Corporation",
        ApiKey = "api_key_acme_123",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    context.Clients.Add(client);

    // Seed surveys
    var survey = new Survey.Infrastructure.Data.Entities.SurveyEntity
    {
        SurveyId = "survey_2024_customer_satisfaction",
        ClientId = "client_acme_corp",
        Name = "Customer Satisfaction Survey 2024",
        Description = "Annual customer satisfaction survey",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    context.Surveys.Add(survey);

    // Seed survey responses
    var responses = new[]
    {
        new Survey.Infrastructure.Data.Entities.SurveyResponseEntity
        {
            ResponseId = "response_001",
            SurveyId = "survey_2024_customer_satisfaction",
            ClientId = "client_acme_corp",
            NpsScore = 9,
            Satisfaction = "very_satisfied",
            CustomFields = "{\"product_used\":\"mobile_app\",\"feature_rating\":8}",
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15",
            IpAddress = "192.168.1.100",
            SessionId = "sess_abc123",
            DeviceType = "mobile",
            ProcessingStatus = "completed",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        },
        new Survey.Infrastructure.Data.Entities.SurveyResponseEntity
        {
            ResponseId = "response_002",
            SurveyId = "survey_2024_customer_satisfaction",
            ClientId = "client_acme_corp",
            NpsScore = 7,
            Satisfaction = "satisfied",
            CustomFields = "{\"product_used\":\"web_app\",\"feature_rating\":6}",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
            IpAddress = "192.168.1.101",
            SessionId = "sess_def456",
            DeviceType = "desktop",
            ProcessingStatus = "completed",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        },
        new Survey.Infrastructure.Data.Entities.SurveyResponseEntity
        {
            ResponseId = "response_003",
            SurveyId = "survey_2024_customer_satisfaction",
            ClientId = "client_acme_corp",
            NpsScore = 10,
            Satisfaction = "very_satisfied",
            CustomFields = "{\"product_used\":\"mobile_app\",\"feature_rating\":9}",
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15",
            IpAddress = "192.168.1.102",
            SessionId = "sess_ghi789",
            DeviceType = "mobile",
            ProcessingStatus = "completed",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        }
    };

    context.SurveyResponses.AddRange(responses.Cast<Survey.Infrastructure.Data.Entities.SurveyResponseEntity>());
    await context.SaveChangesAsync();
}
