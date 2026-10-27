using System.Text.Json;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Operations;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BCMetals.NasAudit.Infrastructure.Identity;

public sealed class ApplicationUserService(
    UserManager<ApplicationUser> userManager,
    AuditDbContext dbContext) : IApplicationUserService
{
    public async Task<IReadOnlyList<ApplicationUserDto>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users.AsNoTracking().OrderBy(x => x.Email).ToListAsync(cancellationToken);
        var result = new List<ApplicationUserDto>(users.Count);
        foreach (var user in users) result.Add(await MapAsync(user));
        return result;
    }

    public async Task<ApplicationUserDto> CreateAsync(
        CreateApplicationUserRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        var role = ValidateRole(request.Role);
        if (email.Length is < 3 or > 256 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new InvalidDataException("El correo no es válido.");
        if (displayName.Length is < 2 or > 256) throw new InvalidDataException("El nombre visible debe contener entre 2 y 256 caracteres.");
        if (await userManager.FindByEmailAsync(email) is not null) throw new InvalidDataException("Ya existe una cuenta con ese correo.");
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            IsEnabled = true,
            MustChangePassword = true
        };
        Ensure(await userManager.CreateAsync(user, request.TemporaryPassword), "crear la cuenta");
        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            Ensure(roleResult, "asignar el rol");
        }
        await RecordAsync("user.create", actor, new { user.Id, user.Email, role }, cancellationToken);
        return await MapAsync(user);
    }

    public async Task<ApplicationUserDto> ChangeRoleAsync(
        Guid userId,
        string role,
        Guid actorId,
        string actor,
        CancellationToken cancellationToken)
    {
        role = ValidateRole(role);
        var user = await RequiredAsync(userId);
        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Count == 1 && currentRoles.Contains(role, StringComparer.Ordinal)) return await MapAsync(user);
        if (currentRoles.Contains(RoleNames.Administrator, StringComparer.Ordinal) && role != RoleNames.Administrator)
            await EnsureAnotherEnabledAdministratorAsync(userId, cancellationToken);
        if (!currentRoles.Contains(role, StringComparer.Ordinal))
            Ensure(await userManager.AddToRoleAsync(user, role), "asignar el rol");
        var obsoleteRoles = currentRoles.Where(x => !string.Equals(x, role, StringComparison.Ordinal)).ToArray();
        if (obsoleteRoles.Length > 0)
            Ensure(await userManager.RemoveFromRolesAsync(user, obsoleteRoles), "retirar el rol anterior");
        await RecordAsync("user.role", actor, new { userId, actorId, role }, cancellationToken);
        return await MapAsync(user);
    }

    public async Task<ApplicationUserDto> SetEnabledAsync(
        Guid userId,
        bool enabled,
        Guid actorId,
        string actor,
        CancellationToken cancellationToken)
    {
        if (userId == actorId && !enabled) throw new InvalidDataException("No puede deshabilitar su propia cuenta.");
        var user = await RequiredAsync(userId);
        if (!enabled && await userManager.IsInRoleAsync(user, RoleNames.Administrator))
            await EnsureAnotherEnabledAdministratorAsync(userId, cancellationToken);
        user.IsEnabled = enabled;
        if (!enabled) user.SecurityStamp = Guid.NewGuid().ToString();
        Ensure(await userManager.UpdateAsync(user), "actualizar el estado de la cuenta");
        await RecordAsync("user.enabled", actor, new { userId, actorId, enabled }, cancellationToken);
        return await MapAsync(user);
    }

    public async Task ResetPasswordAsync(
        Guid userId,
        string temporaryPassword,
        Guid actorId,
        string actor,
        CancellationToken cancellationToken)
    {
        if (userId == actorId) throw new InvalidDataException("Use 'Cambiar mi contraseña' para su propia cuenta.");
        var user = await RequiredAsync(userId);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        Ensure(await userManager.ResetPasswordAsync(user, token, temporaryPassword), "restablecer la contraseña");
        user.MustChangePassword = true;
        user.LastPasswordChangedAtUtc = DateTimeOffset.UtcNow;
        Ensure(await userManager.UpdateAsync(user), "marcar la contraseña temporal");
        await RecordAsync("user.password-reset", actor, new { userId, actorId }, cancellationToken);
    }

    private async Task EnsureAnotherEnabledAdministratorAsync(Guid excludedId, CancellationToken cancellationToken)
    {
        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        if (!administrators.Any(x => x.Id != excludedId && x.IsEnabled))
            throw new InvalidDataException("Debe permanecer al menos un administrador habilitado.");
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task<ApplicationUser> RequiredAsync(Guid userId) =>
        await userManager.FindByIdAsync(userId.ToString()) ?? throw new KeyNotFoundException("La cuenta no existe.");

    private async Task<ApplicationUserDto> MapAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new ApplicationUserDto(
            user.Id,
            user.Email ?? user.UserName ?? user.Id.ToString(),
            user.DisplayName,
            roles.FirstOrDefault() ?? "Sin rol",
            user.IsEnabled,
            user.MustChangePassword,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
            user.LastPasswordChangedAtUtc);
    }

    private async Task RecordAsync(string kind, string actor, object details, CancellationToken cancellationToken)
    {
        dbContext.MaintenanceRuns.Add(new MaintenanceRun
        {
            Kind = kind,
            Status = "completed",
            Actor = actor[..Math.Min(actor.Length, 256)],
            CompletedAtUtc = DateTimeOffset.UtcNow,
            DetailsJson = JsonSerializer.Serialize(details)
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateRole(string role) => role.Trim() switch
    {
        RoleNames.Administrator => RoleNames.Administrator,
        RoleNames.Auditor => RoleNames.Auditor,
        _ => throw new InvalidDataException("El rol debe ser Administrator o Auditor.")
    };

    private static void Ensure(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        throw new InvalidDataException($"No fue posible {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}
