namespace BCMetals.NasAudit.Infrastructure.Operations;

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";
    public bool Enabled { get; set; }
    public int HistoricalDays { get; set; } = 2555;
    public int LiveDays { get; set; } = 2555;
    public int DemoDays { get; set; } = 90;
    public int IngestionMetadataDays { get; set; } = 365;
    public int RejectedEventDays { get; set; } = 365;
    public int BatchSize { get; set; } = 5000;
    public int RunAtUtcHour { get; set; } = 3;
}

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";
    public int QueueDepthWarning { get; set; } = 100;
    public int NoEventWarningHours { get; set; } = 24;
    public int RejectedEventsWarning24Hours { get; set; } = 1;
    public bool BehavioralCandidatesEnabled { get; set; }
    public int BehavioralWindowMinutes { get; set; } = 15;
    public int DeletionCandidateThreshold { get; set; } = 50;
    public int WriteCandidateThreshold { get; set; } = 500;
    public string[] SensitivePathPrefixes { get; set; } = [];
}
