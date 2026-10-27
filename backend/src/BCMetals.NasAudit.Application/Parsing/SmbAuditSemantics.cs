using BCMetals.NasAudit.Domain.Enums;

namespace BCMetals.NasAudit.Application.Parsing;

/// <summary>
/// Reglas puras compartidas por la importación histórica y la ingesta en vivo.
/// Mantiene una sola interpretación para operaciones y rutas SMB.
/// </summary>
public static class SmbAuditSemantics
{
    public static AuditAction Classify(string operation, string? source = null, string? destination = null) =>
        operation.Trim().ToLowerInvariant() switch
        {
            "create" or "creation" or "mkdir" or "mkdirat" => AuditAction.Creation,
            "open" => AuditAction.Open,
            "pread" or "read" => AuditAction.ObservedRead,
            "pwrite" or "write" or "ftruncate" or "truncate" or "set_nt_acl" or "modification" => AuditAction.Write,
            "unlinkat" or "unlink" or "rmdir" or "delete" or "deletion" => AuditAction.Deletion,
            "renameat" or "rename" when IsMove(source, destination) => AuditAction.Move,
            "renameat" or "rename" => AuditAction.Rename,
            "connect" or "disconnect" or "connection" => AuditAction.Connection,
            _ => AuditAction.Unknown
        };

    public static bool IsMove(string? source, string? destination)
    {
        if (IsRecyclePath(destination)) return true;
        var sourceParent = ParentPath(source);
        var destinationParent = ParentPath(destination);
        return sourceParent is not null && destinationParent is not null &&
            !sourceParent.Equals(destinationParent, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ExtractShare(string? path)
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

    public static bool IsAuxiliaryStream(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/');
        return normalized.IndexOf(':', normalized.LastIndexOf('/') + 1) >= 0;
    }

    public static bool IsRecyclePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals("#recycle", StringComparison.OrdinalIgnoreCase));

    public static string? ExtensionOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/');
        var streamSeparator = normalized.IndexOf(':', normalized.LastIndexOf('/') + 1);
        if (streamSeparator >= 0) normalized = normalized[..streamSeparator];
        var extension = Path.GetExtension(normalized);
        return string.IsNullOrWhiteSpace(extension) ? null : extension.ToLowerInvariant();
    }

    private static string? ParentPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var slash = normalized.LastIndexOf('/');
        return slash <= 0 ? null : normalized[..slash];
    }
}
