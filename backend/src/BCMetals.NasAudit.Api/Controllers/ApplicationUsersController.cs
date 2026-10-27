using System.Security.Claims;
using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/v1/admin/users")]
public sealed class ApplicationUsersController(IApplicationUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ApplicationUserDto>>>> List(CancellationToken cancellationToken)
    {
        var result = await users.ListAsync(cancellationToken);
        return Ok(new ApiEnvelope<IReadOnlyList<ApplicationUserDto>>(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiEnvelope<ApplicationUserDto>>> Create(
        CreateApplicationUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(request, ActorEmail(), cancellationToken);
        return Ok(new ApiEnvelope<ApplicationUserDto>(result, "Cuenta creada con contraseña temporal."));
    }

    [HttpPut("{userId:guid}/role")]
    public async Task<ActionResult<ApiEnvelope<ApplicationUserDto>>> ChangeRole(
        Guid userId,
        ChangeApplicationUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.ChangeRoleAsync(userId, request.Role, ActorId(), ActorEmail(), cancellationToken);
        return Ok(new ApiEnvelope<ApplicationUserDto>(result, "Rol actualizado."));
    }

    [HttpPut("{userId:guid}/enabled")]
    public async Task<ActionResult<ApiEnvelope<ApplicationUserDto>>> SetEnabled(
        Guid userId,
        SetApplicationUserEnabledRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.SetEnabledAsync(userId, request.IsEnabled, ActorId(), ActorEmail(), cancellationToken);
        return Ok(new ApiEnvelope<ApplicationUserDto>(result, "Estado de la cuenta actualizado."));
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<ActionResult<ApiEnvelope<object>>> ResetPassword(
        Guid userId,
        ResetApplicationUserPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await users.ResetPasswordAsync(userId, request.TemporaryPassword, ActorId(), ActorEmail(), cancellationToken);
        return Ok(new ApiEnvelope<object>(new { reset = true }, "Contraseña temporal asignada."));
    }

    private Guid ActorId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException();
    }

    private string ActorEmail() => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "administrator";
}
