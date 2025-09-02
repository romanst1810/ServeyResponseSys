# Survey Response Processing System

A scalable, resilient .NET 8 microservice for processing survey responses with real-time analytics and multi-tenant support.

## 🏗️ Architecture Overview

This system implements a modern microservice architecture with the following components:

- **API Layer**: RESTful endpoints for survey response ingestion and metrics retrieval
- **Processing Layer**: Async message queue for background processing
- **Storage Layer**: Dual storage strategy (fast storage + relational database)
- **Analytics Layer**: Real-time NPS metrics calculation and caching
- **Security Layer**: Multi-tenant isolation and rate limiting

## 🚀 Features

### Core Features
- ✅ REST API for survey response ingestion
- ✅ Data validation and sanitization
- ✅ Async processing using in-memory queue (SQS-like)
- ✅ Dual storage: Fast storage (simulated) + Relational storage (SQL Server)
- ✅ Real-time NPS metrics aggregation
- ✅ Error handling and retry logic
- ✅ Comprehensive logging with Serilog

### Advanced Features
- ✅ Multi-tenant data isolation
- ✅ Rate limiting per client
- ✅ Data encryption for sensitive fields
- ✅ Circuit breaker pattern (simulated)
- ✅ Background job processing
- ✅ Health monitoring and metrics

## 📋 Prerequisites

- .NET 8.0 SDK
- Docker Desktop with Docker Compose (for containerized deployment)
- SQL Server (optional; development falls back to in-memory if no connection string)
- Redis (optional, for production caching)

## 🛠️ Installation & Setup

### Option 1: Local Development

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd SurveyResponseSystem
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Run the application**
   ```bash
   dotnet build
   # Optional (PowerShell): set explicit URLs
   $env:ASPNETCORE_ENVIRONMENT="Development"
   $env:ASPNETCORE_URLS="http://localhost:5000"
   dotnet run --project .\SurveyApi\SurveyApi.csproj
   ```

4. **Access the API**
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger

### Option 2: Docker Deployment

1. **Build and run with Docker Compose**
   ```bash
   docker compose up -d --build
   ```

2. **Access the services**
   - API: http://localhost:5000
   - SQL Server: localhost:1433
   - Redis: localhost:6379

## 📚 API Documentation

### Authentication
All API endpoints require authentication using API keys:
```
Authorization: Bearer {api_key}
X-Client-ID: {client_id}
```

### Endpoints

#### 1. Submit Survey Response
```http
POST /api/v1/survey-responses
Content-Type: application/json

{
  "surveyId": "survey_12345",
  "clientId": "client_67890",
  "responseId": "response_abc123",
  "responses": {
    "nps_score": 8,
    "satisfaction": "satisfied",
    "custom_fields": {
      "product_used": "mobile_app",
      "feature_rating": 9
    }
  },
  "metadata": {
    "timestamp": "2024-01-01T10:00:00Z",
    "user_agent": "Mozilla/5.0...",
    "ip_address": "192.168.1.100",
    "device_type": "mobile"
  }
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "responseId": "response_abc123",
    "status": "accepted",
    "processingId": "proc_123456",
    "estimatedProcessingTime": "30 seconds",
    "createdAt": "2024-01-01T10:00:00Z"
  },
  "message": "Survey response accepted for processing"
}
```

#### 2. Get NPS Metrics
```http
GET /api/v1/metrics/nps/{clientId}?surveyId={surveyId}&period=day
```

**Response:**
```json
{
  "success": true,
  "data": {
    "clientId": "client_67890",
    "surveyId": "survey_12345",
    "period": "day",
    "metrics": {
      "currentNps": 7.8,
      "previousNps": 7.5,
      "change": 0.3,
      "changePercentage": 4.0,
      "totalResponses": 1250,
      "responseCount": 45,
      "satisfactionDistribution": {
        "very_satisfied": 15,
        "satisfied": 20,
        "neutral": 8,
        "dissatisfied": 2,
        "very_dissatisfied": 0
      },
      "npsBreakdown": {
        "promoters": 35,
        "passives": 8,
        "detractors": 2
      }
    },
    "trends": {
      "direction": "increasing",
      "confidence": 0.85,
      "lastUpdated": "2024-01-01T10:00:00Z"
    }
  }
}
```

#### 3. Get Processing Status
```http
GET /api/v1/processing/{processingId}
```

#### 4. Get Client Responses
```http
GET /api/v1/clients/{clientId}/responses?skip=0&take=100
```

#### 5. Health Check
```http
GET /api/v1/health
```

## 🧪 Testing

### Unit Tests
```bash
cd Survey.Tests
dotnet test
```

### Integration Tests
```bash
# Run with test database
dotnet test --filter Category=Integration
```

### API Testing with curl

1. **Submit a survey response:**
```bash
curl -X POST http://localhost:5000/api/v1/survey-responses \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer your-api-key" \
  -H "X-Client-ID: client_67890" \
  -d '{
    "surveyId": "survey_12345",
    "clientId": "client_67890",
    "responseId": "response_test_001",
    "responses": {
      "nps_score": 9,
      "satisfaction": "very_satisfied",
      "custom_fields": {
        "product_used": "mobile_app",
        "feature_rating": 10
      }
    },
    "metadata": {
      "timestamp": "2024-01-01T10:00:00Z",
      "user_agent": "curl/7.68.0",
      "ip_address": "127.0.0.1",
      "device_type": "desktop"
    }
  }'
```

2. **Get NPS metrics:**
```bash
curl -X GET "http://localhost:5000/api/v1/metrics/nps/client_67890?period=day" \
  -H "Authorization: Bearer your-api-key" \
  -H "X-Client-ID: client_67890"
```

3. **Health check:**
```bash
curl -X GET http://localhost:5000/api/v1/health
```

## 🔧 Configuration

### Environment Variables
```bash
# Database
ConnectionStrings__DefaultConnection=Server=localhost;Database=SurveyResponseDb;Trusted_Connection=true;

# Rate Limiting
RateLimiting__PermitLimit=1000
RateLimiting__Window=01:00:00

# Logging
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Override__Microsoft=Warning
```

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=SurveyResponseDb;Trusted_Connection=true;"
  },
  "RateLimiting": {
    "PermitLimit": 1000,
    "Window": "01:00:00"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning"
      }
    }
  }
}
```

## 📊 Monitoring & Observability

### Health Checks
- **Endpoint**: `/api/v1/health`
- **Database connectivity**
- **Queue status**
- **Cache health**

### Logging
- **Structured logging** with Serilog
- **Console and file output**
- **Request/response correlation**
- **Error tracking and alerting**

### Metrics
- **Request rate**
- **Response times**
- **Error rates**
- **Queue depth**

## 🔒 Security

### Multi-Tenant Isolation
- **Database per tenant** (enterprise)
- **Schema-based isolation** (shared)
- **Row-level security** (SQL Server RLS)

### Authentication & Authorization
- **API key authentication**
- **Client-specific access control**
- **Rate limiting per client**

### Data Protection
- **Encryption at rest**
- **Encryption in transit**
- **Sensitive data masking**

## 🚀 Deployment

### Production Deployment
1. **Build the Docker image:**
   ```bash
   docker build -t survey-response-api .
   ```

2. **Deploy with Docker Compose:**
   ```bash
   docker compose up -d --build
   ```

3. **Configure environment variables:**
   ```bash
   export ASPNETCORE_ENVIRONMENT=Production
   export ConnectionStrings__DefaultConnection="your-production-connection-string"
   ```

4. **Healthcheck note**
   - The Dockerfile healthcheck targets `/health`, while the API exposes `/api/v1/health`. Update the Dockerfile if you want container health to reflect the API endpoint.

### Azure Deployment
1. **Create Azure Container Registry**
2. **Push Docker image**
3. **Deploy to Azure Container Instances or AKS**

## 📈 Performance & Scaling

### Horizontal Scaling
- **Stateless API services**
- **Auto-scaling based on CPU/memory**
- **Load balancing with health checks**

### Database Scaling
- **Read replicas for analytics**
- **Partitioning by date and client**
- **Connection pooling**

### Caching Strategy
- **Redis for session data**
- **In-memory caching for metrics**
- **CDN for static assets**

## 🐛 Troubleshooting

### Common Issues

1. **Database Connection Errors**
   - Check connection string
   - Verify SQL Server is running
   - Check firewall settings

2. **Rate Limiting**
   - Monitor rate limit headers
   - Implement exponential backoff
   - Contact support for limit increases

3. **Processing Delays**
   - Check queue depth
   - Monitor background job status
   - Verify processing workers are running

### Logs
- **Application logs**: `/app/logs/` (mounted to `./logs` with Docker Compose)
- **Docker logs**: `docker compose logs survey-api`
- **Database logs**: Check SQL Server logs

## ⚙️ Load Testing

This project includes a k6-based load test.

- Script: `loadtest/k6/survey-load.js`
- Runner (Docker): `docker-compose.loadtest.yml`
- Results: `loadtest/results/summary.json`

### Run prerequisites
- API running locally (e.g., `docker compose up -d --build` or `dotnet run --project .\SurveyApi\SurveyApi.csproj`)
- Docker Desktop

### Run with Docker (Windows PowerShell)
```powershell
docker compose -f docker-compose.loadtest.yml run --rm `
  -e BASE_URL=http://host.docker.internal:5000 `
  -e CLIENT_ID=client_acme_corp `
  -e API_KEY=dummy `
  -e VUS=20 -e DURATION=1m k6
```

### Run with Docker (bash)
```bash
docker compose -f docker-compose.loadtest.yml run --rm \
  -e BASE_URL=http://host.docker.internal:5000 \
  -e CLIENT_ID=client_acme_corp \
  -e API_KEY=dummy \
  -e VUS=20 -e DURATION=1m k6
```

Notes:
- Use `http://host.docker.internal:5000` as the API base URL when the API runs on the host machine.
- Adjust `VUS` and `DURATION` to change load (e.g., `VUS=100`, `DURATION=5m`).
- The k6 summary is exported to `loadtest/results/summary.json`.


