namespace BCMetals.NasAudit.Domain.Enums;

public enum AuditAction
{
    Unknown = 0,
    Creation = 1,
    Open = 2,
    Write = 3,
    Deletion = 4,
    Rename = 5,
    Move = 6,
    Connection = 7,
    ObservedRead = 8
}

