namespace BCMetals.NasAudit.Application.Common;

public sealed record ApiEnvelope<T>(T Data, string? Message = null);

