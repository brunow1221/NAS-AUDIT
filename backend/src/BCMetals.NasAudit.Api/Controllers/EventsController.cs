using System.Text.Json;
using System.Text.Json.Serialization;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Events;
using BCMetals.NasAudit.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/events")]
public sealed class EventsController(IAuditReadService auditReadService, ILiveEventPublisher liveEventPublisher) : ControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = CreateSseJsonOptions();

    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<PagedResult<AuditEventDto>>>> List(
        [FromQuery] EventQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(request);
        var result = await auditReadService.GetEventsAsync(query, cancellationToken);
        return Ok(new ApiEnvelope<PagedResult<AuditEventDto>>(result));
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] EventQueryRequest request, CancellationToken cancellationToken)
    {
        var export = await auditReadService.ExportEventsAsync(BuildQuery(request), cancellationToken);
        Response.Headers.Append("X-Exported-Rows", export.RowCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return File(export.Content, "text/csv; charset=utf-8", export.FileName);
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-store";
        Response.Headers.Append("X-Accel-Buffering", "no");
        await Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        // Register before replaying the recent window so events committed during the query
        // are queued instead of falling through the replay/live hand-off gap.
        await using var liveSubscription = liveEventPublisher.Subscribe();
        var recent = await auditReadService.GetRecentLiveEventsAsync(100, cancellationToken);
        var lastEventId = Request.Headers["Last-Event-ID"].ToString();
        var start = string.IsNullOrWhiteSpace(lastEventId)
            ? 0
            : recent.ToList().FindIndex(x => x.Id.ToString() == lastEventId) + 1;
        if (start < 0) start = 0;
        foreach (var item in recent.Skip(start)) await WriteSseAsync(item, cancellationToken);

        await using var subscription = liveSubscription.ReadAllAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
        Task<bool>? pendingMove = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            pendingMove ??= subscription.MoveNextAsync().AsTask();
            var keepAlive = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            var completed = await Task.WhenAny(pendingMove, keepAlive);
            if (completed == pendingMove)
            {
                if (!await pendingMove) break;
                await WriteSseAsync(subscription.Current, cancellationToken);
                pendingMove = null;
            }
            else
            {
                await Response.WriteAsync($": keepalive {DateTimeOffset.UtcNow:O}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiEnvelope<AuditEventDto>>> Detail(Guid id, CancellationToken cancellationToken)
    {
        var result = await auditReadService.GetEventAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(new ApiEnvelope<AuditEventDto>(result));
    }

    private static EventQuery BuildQuery(EventQueryRequest request) => new(
        request.From?.ToUniversalTime(), request.To?.ToUniversalTime(), request.User,
        request.ClientIp, request.Operation, request.Action, request.Share, request.FileName,
        request.Path, request.Extension, request.Result, request.Source, request.Confidence,
        request.Search, request.Mode, Math.Max(request.Page, 1), Math.Clamp(request.PageSize, 1, 100),
        request.SortBy ?? "timestamp", request.Descending);

    private async Task WriteSseAsync(AuditEventDto auditEvent, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"id: {auditEvent.Id}\n", cancellationToken);
        await Response.WriteAsync("event: audit-event\n", cancellationToken);
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(auditEvent, SseJsonOptions)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private static JsonSerializerOptions CreateSseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class EventQueryRequest
{
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? User { get; init; }
    public string? ClientIp { get; init; }
    public string? Operation { get; init; }
    public AuditAction? Action { get; init; }
    public string? Share { get; init; }
    public string? FileName { get; init; }
    public string? Path { get; init; }
    public string? Extension { get; init; }
    public string? Result { get; init; }
    public string? Source { get; init; }
    public InterpretationConfidence? Confidence { get; init; }
    public string? Search { get; init; }
    public DataMode Mode { get; init; } = DataMode.Historical;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? SortBy { get; init; } = "timestamp";
    public bool Descending { get; init; } = true;
}

