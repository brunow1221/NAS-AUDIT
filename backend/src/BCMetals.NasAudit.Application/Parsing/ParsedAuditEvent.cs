using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Parsing;

public sealed record ParsedAuditEvent(
    string? SmbUser,
    string? ClientIp,
    string Operation,
    AuditAction Action,
    InterpretationConfidence Confidence,
    string Result,
    string? ShareName,
    string? FilePath,
    string? SourcePath,
    string? DestinationPath,
    string? ObjectType,
    bool IsAuxiliaryStream,
    bool IsRecyclePath);

public interface ISambaMessageParser
{
    bool TryParse(string rawMessage, out ParsedAuditEvent? parsed);
}

