using System.Text.Json;

namespace BCMetals.NasAudit.Application.Ingestion;

public sealed record CollectorEventRequest(
    string Cursor,
    string? BootId,
    DateTimeOffset TimestampUtc,
    string Message,
    IReadOnlyDictionary<string, JsonElement>? Metadata);

public sealed record IngestionBatchRequest(
    string CollectorId,
    Guid BatchId,
    IReadOnlyList<CollectorEventRequest> Events);

public sealed record IngestionBatchResult(
    Guid BatchId,
    int Accepted,
    int Duplicates,
    int Rejected,
    string? ConfirmedCursor,
    bool Replay);

public sealed record CollectorHeartbeatRequest(
    string CollectorId,
    string Status,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset? LastEventUtc,
    int QueueDepth,
    string? Message,
    IReadOnlyDictionary<string, JsonElement>? Metadata);

public sealed record CollectorStatusDto(
    string Status,
    string Label,
    string? CollectorId,
    DateTimeOffset? LastHeartbeatUtc,
    DateTimeOffset? LastEventUtc,
    string? ConfirmedCursor,
    int QueueDepth,
    string? Message);
