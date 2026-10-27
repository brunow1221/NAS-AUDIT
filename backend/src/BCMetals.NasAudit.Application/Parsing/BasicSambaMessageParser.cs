using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Parsing;

/// <summary>
/// Parser versionado para el formato audit_full emitido por Samba/UGOS.
/// La cabecera se consume campo a campo y cada operación aplica su propia gramática;
/// los segmentos libres de ruta nunca se dividen con un Split global.
/// </summary>
public sealed class BasicSambaMessageParser : ISambaMessageParser
{
    public const string Version = "phase2-ugos-v2";
    private const string Marker = "ugreen_syslog|";

    public bool TryParse(string rawMessage, out ParsedAuditEvent? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(rawMessage)) return false;

        var markerIndex = rawMessage.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0) return false;

        var message = rawMessage[markerIndex..].Trim();
        var cursor = 0;
        if (!TryReadDelimited(message, ref cursor, out var identifier) ||
            !identifier.Equals("ugreen_syslog", StringComparison.OrdinalIgnoreCase) ||
            !TryReadDelimited(message, ref cursor, out var user) ||
            !TryReadDelimited(message, ref cursor, out var ip) ||
            !TryReadDelimited(message, ref cursor, out var operation) ||
            !TryReadValue(message, ref cursor, out var result, out var tail))
        {
            return false;
        }

        operation = operation.Trim().ToLowerInvariant();
        if (operation.Length == 0) return false;

        string? path = null;
        string? source = null;
        string? destination = null;
        string? objectType = null;
        var action = AuditAction.Unknown;
        var confidence = InterpretationConfidence.Unknown;

        switch (operation)
        {
            case "connect":
            case "disconnect":
                action = AuditAction.Connection;
                confidence = InterpretationConfidence.Confirmed;
                path = NullIfEmpty(tail);
                break;
            case "create_file":
                if (!TryParseCreateFile(tail, out objectType, out var disposition, out path))
                {
                    path = NullIfEmpty(tail);
                    confidence = InterpretationConfidence.Low;
                    break;
                }

                action = disposition.ToLowerInvariant() switch
                {
                    "create" or "created" => AuditAction.Creation,
                    "open" or "opened" => AuditAction.Open,
                    _ => AuditAction.Unknown
                };
                confidence = action == AuditAction.Unknown
                    ? InterpretationConfidence.Low
                    : InterpretationConfidence.Confirmed;
                break;
            case "pwrite":
            case "write":
            case "ftruncate":
            case "truncate":
            case "set_nt_acl":
                action = AuditAction.Write;
                confidence = InterpretationConfidence.Confirmed;
                path = NullIfEmpty(tail);
                break;
            case "pread":
            case "read":
                action = AuditAction.ObservedRead;
                confidence = InterpretationConfidence.Medium;
                path = NullIfEmpty(tail);
                break;
            case "unlinkat":
            case "unlink":
            case "rmdir":
                action = AuditAction.Deletion;
                confidence = InterpretationConfidence.Confirmed;
                path = NullIfEmpty(tail);
                break;
            case "mkdirat":
            case "mkdir":
                action = AuditAction.Creation;
                confidence = InterpretationConfidence.Confirmed;
                objectType = "directory";
                path = NullIfEmpty(tail);
                break;
            case "renameat":
            case "rename":
                if (!TryParseRename(tail, out source, out destination))
                {
                    path = NullIfEmpty(tail);
                    confidence = InterpretationConfidence.Low;
                    break;
                }

                path = destination;
                action = IsMove(source, destination) ? AuditAction.Move : AuditAction.Rename;
                confidence = InterpretationConfidence.Confirmed;
                break;
            default:
                path = NullIfEmpty(tail);
                confidence = InterpretationConfidence.Low;
                break;
        }

        var inspectedPath = destination ?? source ?? path;
        parsed = new ParsedAuditEvent(
            NullIfEmpty(user),
            NullIfEmpty(ip),
            operation,
            action,
            confidence,
            string.IsNullOrWhiteSpace(result) ? "unknown" : result.Trim(),
            ExtractShare(inspectedPath),
            path,
            source,
            destination,
            objectType,
            IsAuxiliaryStream(inspectedPath),
            IsRecyclePath(source) || IsRecyclePath(destination) || IsRecyclePath(path));
        return true;
    }

    private static bool TryParseCreateFile(string tail, out string? objectType, out string disposition, out string? path)
    {
        objectType = null;
        disposition = string.Empty;
        path = null;
        var cursor = 0;
        if (!TryReadDelimited(tail, ref cursor, out _) ||
            !TryReadDelimited(tail, ref cursor, out var type) ||
            !TryReadDelimited(tail, ref cursor, out disposition)) return false;
        objectType = NullIfEmpty(type);
        path = NullIfEmpty(tail[cursor..]);
        return path is not null;
    }

    private static bool TryParseRename(string tail, out string? source, out string? destination)
    {
        source = null;
        destination = null;
        var separator = tail.IndexOf('|');
        if (separator <= 0 || separator == tail.Length - 1) return false;
        source = NullIfEmpty(tail[..separator]);
        destination = NullIfEmpty(tail[(separator + 1)..]);
        return source is not null && destination is not null;
    }

    private static bool TryReadDelimited(string value, ref int cursor, out string field)
    {
        field = string.Empty;
        if (cursor > value.Length) return false;
        var separator = value.IndexOf('|', cursor);
        if (separator < 0) return false;
        field = value[cursor..separator].Trim();
        cursor = separator + 1;
        return true;
    }

    private static bool TryReadValue(string value, ref int cursor, out string field, out string tail)
    {
        field = string.Empty;
        tail = string.Empty;
        if (cursor > value.Length) return false;
        var separator = value.IndexOf('|', cursor);
        if (separator < 0)
        {
            field = value[cursor..].Trim();
            cursor = value.Length;
            return true;
        }

        field = value[cursor..separator].Trim();
        cursor = separator + 1;
        tail = value[cursor..].Trim();
        return true;
    }

    private static bool IsMove(string? source, string? destination)
    {
        if (IsRecyclePath(destination)) return true;
        var sourceParent = ParentPath(source);
        var destinationParent = ParentPath(destination);
        return sourceParent is not null && destinationParent is not null &&
            !sourceParent.Equals(destinationParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ParentPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var slash = normalized.LastIndexOf('/');
        return slash <= 0 ? null : normalized[..slash];
    }

    private static bool IsAuxiliaryStream(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/');
        var fileNameStart = normalized.LastIndexOf('/') + 1;
        return normalized.IndexOf(':', fileNameStart) >= 0;
    }

    private static bool IsRecyclePath(string? path) =>
        path?.Replace('\\', '/').Contains("/#recycle/", StringComparison.OrdinalIgnoreCase) == true ||
        path?.EndsWith("/#recycle", StringComparison.OrdinalIgnoreCase) == true;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ExtractShare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/');
        const string prefix = "/volume1/";
        var index = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var remainder = normalized[(index + prefix.Length)..];
        var slash = remainder.IndexOf('/');
        return slash < 0 ? remainder : remainder[..slash];
    }
}
