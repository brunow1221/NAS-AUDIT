using System.Collections.Concurrent;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Operations;

namespace BCMetals.NasAudit.Infrastructure.Operations;

public sealed class OperationalEventBuffer : IOperationalEventBuffer
{
    private const int Capacity = 100;
    private readonly ConcurrentQueue<OperationalEventDto> _events = new();

    public void Record(string severity, string code, string message)
    {
        _events.Enqueue(new OperationalEventDto(
            DateTimeOffset.UtcNow,
            severity[..Math.Min(severity.Length, 20)],
            code[..Math.Min(code.Length, 80)],
            message[..Math.Min(message.Length, 500)]));
        while (_events.Count > Capacity) _events.TryDequeue(out _);
    }

    public IReadOnlyList<OperationalEventDto> Snapshot(int limit = 20) =>
        _events.Reverse().Take(Math.Clamp(limit, 1, Capacity)).ToList();
}
