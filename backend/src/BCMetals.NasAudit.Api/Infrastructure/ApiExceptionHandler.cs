using BCMetals.NasAudit.Application.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BCMetals.NasAudit.Api.Infrastructure;

public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IOperationalEventBuffer operationalEvents,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogUnexpected = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1001, "UnhandledApiError"),
        "Error no controlado al procesar {Path}");

    private static readonly Action<ILogger, string, string, Exception?> LogRejected = LoggerMessage.Define<string, string>(
        LogLevel.Warning,
        new EventId(1002, "RejectedApiRequest"),
        "Solicitud rechazada en {Path}: {Reason}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            InvalidDataException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status409Conflict,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        if (status >= 500)
        {
            LogUnexpected(logger, httpContext.Request.Path, exception);
            operationalEvents.Record("error", "api.unhandled", $"Error interno al procesar {httpContext.Request.Path}.");
        }
        else
        {
            LogRejected(logger, httpContext.Request.Path, exception.Message, null);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status switch { 400 => "Solicitud inválida", 403 => "Acceso denegado", 404 => "No encontrado", 409 => "Operación no disponible", _ => "Error interno" },
                Detail = status is 400 or 403 or 404 or 409 ? exception.Message : "La operación no pudo completarse. Revise los registros del servicio."
            },
            Exception = exception
        });
    }
}

