using BCMetals.NasAudit.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BCMetals.NasAudit.Infrastructure.Operations;

public sealed class RetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    IOperationalEventBuffer events,
    ILogger<RetentionBackgroundService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogRetentionFailure = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(5001, "ScheduledRetentionFailed"),
        "La retención programada falló");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.Value;
            var delay = DelayUntilNextRun(settings.RunAtUtcHour);
            await Task.Delay(delay, stoppingToken);
            if (!settings.Enabled) continue;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var retention = scope.ServiceProvider.GetRequiredService<IRetentionService>();
                var result = await retention.RunAsync("system:scheduler", stoppingToken);
                events.Record("information", "retention.completed", $"Retención completada; {result.DeletedEvents} eventos eliminados según política.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                events.Record("error", "retention.failed", "La ejecución programada de retención falló. Revise los registros del servicio.");
                LogRetentionFailure(logger, exception);
            }
        }
    }

    public static TimeSpan DelayUntilNextRun(int utcHour)
    {
        var now = DateTimeOffset.UtcNow;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, utcHour, 0, 0, TimeSpan.Zero);
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
