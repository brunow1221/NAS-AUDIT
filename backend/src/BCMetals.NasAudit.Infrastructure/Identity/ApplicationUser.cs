using Microsoft.AspNetCore.Identity;

namespace BCMetals.NasAudit.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public DateTimeOffset? LastPasswordChangedAtUtc { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
}

