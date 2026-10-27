using System.ComponentModel.DataAnnotations;

namespace BCMetals.NasAudit.Application.Operations;

public sealed record ApplicationUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsEnabled,
    bool MustChangePassword,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset? LastPasswordChangedAtUtc);

public sealed record CreateApplicationUserRequest(
    [param: Required, EmailAddress, StringLength(256)] string Email,
    [param: Required, StringLength(256, MinimumLength = 2)] string DisplayName,
    [param: Required, RegularExpression("^(Administrator|Auditor)$")] string Role,
    [param: Required, MinLength(14)] string TemporaryPassword);

public sealed record ChangeApplicationUserRoleRequest(
    [param: Required, RegularExpression("^(Administrator|Auditor)$")] string Role);
public sealed record SetApplicationUserEnabledRequest(bool IsEnabled);
public sealed record ResetApplicationUserPasswordRequest([param: Required, MinLength(14)] string TemporaryPassword);
