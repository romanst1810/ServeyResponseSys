using Microsoft.EntityFrameworkCore;
using Survey.Core.Models;
using Survey.Infrastructure.Data.Entities;

namespace Survey.Infrastructure.Data;

public class SurveyResponseDbContext : DbContext
{
    public SurveyResponseDbContext(DbContextOptions<SurveyResponseDbContext> options) : base(options)
    {
    }

    public DbSet<SurveyResponseEntity> SurveyResponses { get; set; }
    public DbSet<ClientEntity> Clients { get; set; }
    public DbSet<SurveyEntity> Surveys { get; set; }
    public DbSet<ClientMetricsEntity> ClientMetrics { get; set; }
    public DbSet<ProcessingQueueEntity> ProcessingQueue { get; set; }
    public DbSet<AuditLogEntity> AuditLog { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SurveyResponse entity configuration
        modelBuilder.Entity<SurveyResponseEntity>(entity =>
        {
            entity.HasKey(e => e.ResponseId);
            entity.Property(e => e.ResponseId).HasMaxLength(50);
            entity.Property(e => e.SurveyId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NpsScore).HasPrecision(3, 2);
            entity.Property(e => e.Satisfaction).HasMaxLength(50);
            entity.Property(e => e.CustomFields).HasColumnType("nvarchar(max)");
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.DeviceType).HasMaxLength(20);
            entity.Property(e => e.ProcessingStatus).HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.SurveyId);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ProcessingStatus);
        });

        // Client entity configuration
        modelBuilder.Entity<ClientEntity>(entity =>
        {
            entity.HasKey(e => e.ClientId);
            entity.Property(e => e.ClientId).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.ApiKey).HasMaxLength(255).IsRequired();
            entity.Property(e => e.TenantId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EncryptionKey).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ApiKey).IsUnique();
            entity.HasIndex(e => e.TenantId);
        });

        // Survey entity configuration
        modelBuilder.Entity<SurveyEntity>(entity =>
        {
            entity.HasKey(e => e.SurveyId);
            entity.Property(e => e.SurveyId).HasMaxLength(50);
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnType("nvarchar(max)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.IsActive);
        });

        // ClientMetrics entity configuration
        modelBuilder.Entity<ClientMetricsEntity>(entity =>
        {
            entity.HasKey(e => e.MetricId);
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SurveyId).HasMaxLength(50);
            entity.Property(e => e.MetricDate).IsRequired();
            entity.Property(e => e.TotalResponses).HasDefaultValue(0);
            entity.Property(e => e.AverageNps).HasPrecision(3, 2);
            entity.Property(e => e.SatisfactionDistribution).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ResponseRate).HasPrecision(5, 4);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.SurveyId);
            entity.HasIndex(e => e.MetricDate);
        });

        // ProcessingQueue entity configuration
        modelBuilder.Entity<ProcessingQueueEntity>(entity =>
        {
            entity.HasKey(e => e.ProcessingId);
            entity.Property(e => e.ProcessingId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ResponseId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ClientId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SurveyId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("pending");
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.MaxRetries).HasDefaultValue(3);
            entity.Property(e => e.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ScheduledAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.ClientId);
        });

        // AuditLog entity configuration
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.ClientId).HasMaxLength(50);
            entity.Property(e => e.SurveyId).HasMaxLength(50);
            entity.Property(e => e.ResponseId).HasMaxLength(50);
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OldValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.NewValues).HasColumnType("nvarchar(max)");
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Action);
        });
    }
}
