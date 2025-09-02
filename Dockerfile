# Use the official .NET 8.0 runtime image as the base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project files
COPY ["SurveyApi/SurveyApi.csproj", "SurveyApi/"]
COPY ["Survey.Core/Survey.Core.csproj", "Survey.Core/"]
COPY ["Survey.Infrastructure/Survey.Infrastructure.csproj", "Survey.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "SurveyApi/SurveyApi.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
WORKDIR "/src/SurveyApi"
RUN dotnet build "SurveyApi.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "SurveyApi.csproj" -c Release -o /app/publish

# Build the final runtime image
FROM base AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Copy the published application
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80

# Create a non-root user
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "SurveyApi.dll"]
