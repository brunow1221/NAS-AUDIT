using BCMetals.NasAudit.Application.Operations;

namespace BCMetals.NasAudit.Application.Abstractions;

public interface IApplicationUserService
{
    Task<IReadOnlyList<ApplicationUserDto>> ListAsync(CancellationToken cancellationToken);
    Task<ApplicationUserDto> CreateAsync(CreateApplicationUserRequest request, string actor, CancellationToken cancellationToken);
    Task<ApplicationUserDto> ChangeRoleAsync(Guid userId, string role, Guid actorId, string actor, CancellationToken cancellationToken);
    Task<ApplicationUserDto> SetEnabledAsync(Guid userId, bool enabled, Guid actorId, string actor, CancellationToken cancellationToken);
    Task ResetPasswordAsync(Guid userId, string temporaryPassword, Guid actorId, string actor, CancellationToken cancellationToken);
}
