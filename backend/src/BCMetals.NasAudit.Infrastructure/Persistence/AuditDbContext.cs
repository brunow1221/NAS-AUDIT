using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BCMetals.NasAudit.Infrastructure.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<EventSource> EventSources => Set<EventSource>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<CollectorCheckpoint> CollectorCheckpoints => Set<CollectorCheckpoint>();
    public DbSet<CollectorHeartbeat> CollectorHeartbeats => Set<CollectorHeartbeat>();
    public DbSet<IngestionBatch> IngestionBatches => Set<IngestionBatch>();
    public DbSet<RejectedIngestionEvent> RejectedIngestionEvents => Set<RejectedIngestionEvent>();
    public DbSet<MaintenanceRun> MaintenanceRuns => Set<MaintenanceRun>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("nas_audit");

        builder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Operation).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OperationResult).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SmbUser).HasMaxLength(256);
            entity.Property(x => x.ClientIp).HasMaxLength(64);
            entity.Property(x => x.ShareName).HasMaxLength(256);
            entity.Property(x => x.FilePath).HasMaxLength(4096);
            entity.Property(x => x.SourcePath).HasMaxLength(4096);
            entity.Property(x => x.DestinationPath).HasMaxLength(4096);
            entity.Property(x => x.FileExtension).HasMaxLength(64);
            entity.Property(x => x.ObjectType).HasMaxLength(80);
            entity.Property(x => x.JournalCursor).HasMaxLength(2048);
            entity.Property(x => x.BootId).HasMaxLength(80);
            entity.Property(x => x.DeduplicationKey).HasMaxLength(160).IsRequired();
            entity.Property(x => x.ParserVersion).HasMaxLength(64).IsRequired();
            entity.Property(x => x.InterpretedAction).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.InterpretationConfidence).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.DataMode).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.DeduplicationKey).IsUnique();
            entity.HasIndex(x => x.EventTimestampUtc).IsDescending();
            entity.HasIndex(x => new { x.SmbUser, x.EventTimestampUtc });
            entity.HasIndex(x => new { x.InterpretedAction, x.EventTimestampUtc });
            entity.HasIndex(x => new { x.ShareName, x.EventTimestampUtc });
            entity.HasIndex(x => x.EventSourceId);
            entity.HasIndex(x => x.FilePath).HasMethod("hash");
            entity.HasIndex(x => x.SourcePath).HasMethod("hash");
            entity.HasIndex(x => x.DestinationPath).HasMethod("hash");
            entity.HasIndex(x => new { x.FileExtension, x.EventTimestampUtc });
            entity.HasIndex(x => new { x.DataMode, x.EventTimestampUtc }).IsDescending(false, true);
            entity.HasIndex(x => new { x.DataMode, x.SmbUser, x.EventTimestampUtc }).IsDescending(false, false, true);
            entity.HasIndex(x => new { x.DataMode, x.InterpretedAction, x.EventTimestampUtc }).IsDescending(false, false, true);
            entity.HasOne(x => x.EventSource)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.EventSourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EventSource>(entity =>
        {
            entity.ToTable("event_sources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<ImportJob>(entity =>
        {
            entity.ToTable("import_jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            entity.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Warnings).HasMaxLength(8000);
            entity.HasIndex(x => x.ContentHash);
            entity.HasIndex(x => x.StartedAtUtc).IsDescending();
        });

        builder.Entity<CollectorCheckpoint>(entity =>
        {
            entity.ToTable("collector_checkpoints");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CollectorId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Cursor).HasMaxLength(2048);
            entity.Property(x => x.BootId).HasMaxLength(128);
            entity.HasIndex(x => x.CollectorId).IsUnique();
            entity.HasOne(x => x.EventSource).WithMany().HasForeignKey(x => x.EventSourceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CollectorHeartbeat>(entity =>
        {
            entity.ToTable("collector_heartbeats");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CollectorId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(x => x.CollectorId).IsUnique();
            entity.HasIndex(x => x.ObservedAtUtc).IsDescending();
        });

        builder.Entity<IngestionBatch>(entity =>
        {
            entity.ToTable("ingestion_batches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CollectorId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ConfirmedCursor).HasMaxLength(2048);
            entity.HasIndex(x => new { x.CollectorId, x.BatchId }).IsUnique();
            entity.HasIndex(x => x.ReceivedAtUtc).IsDescending();
        });

        builder.Entity<RejectedIngestionEvent>(entity =>
        {
            entity.ToTable("rejected_ingestion_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CollectorId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Cursor).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.BootId).HasMaxLength(128);
            entity.Property(x => x.Reason).HasMaxLength(160).IsRequired();
            entity.Property(x => x.MetadataJson).HasColumnType("jsonb");
            entity.Property(x => x.DeduplicationKey).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.DeduplicationKey).IsUnique();
            entity.HasIndex(x => x.ReceivedAtUtc).IsDescending();
        });

        builder.Entity<MaintenanceRun>(entity =>
        {
            entity.ToTable("maintenance_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Actor).HasMaxLength(256).IsRequired();
            entity.Property(x => x.DetailsJson).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.Kind, x.StartedAtUtc }).IsDescending(false, true);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(256);
            entity.Property(x => x.IsEnabled).HasDefaultValue(true);
            entity.Property(x => x.MustChangePassword).HasDefaultValue(true);
            entity.HasIndex(x => x.IsEnabled);
        });
    }
}

