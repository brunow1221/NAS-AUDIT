using System.Security.Claims;
using BCMetals.NasAudit.Application.Common;
using BCMetals.NasAudit.Application.Operations;
using BCMetals.NasAudit.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BCMetals.NasAudit.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public ActionResult<ApiEnvelope<object>> Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new ApiEnvelope<object>(new { token = tokens.RequestToken }));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<ApiEnvelope<UserSessionDto>>> Login(LoginRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Unauthorized(new ProblemDetails { Title = "Credenciales inválidas", Status = 401 });
        }
        if (!user.IsEnabled)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = "Cuenta deshabilitada", Status = 403 });
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, false, true);
        if (result.IsLockedOut)
        {
            return StatusCode(423, new ProblemDetails { Title = "Cuenta bloqueada temporalmente", Status = 423 });
        }
        if (!result.Succeeded)
        {
            return Unauthorized(new ProblemDetails { Title = "Credenciales inválidas", Status = 401 });
        }

        user.LastLoginAtUtc = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new ApiEnvelope<UserSessionDto>(new UserSessionDto(user.Email!, user.DisplayName, roles.ToArray(), user.MustChangePassword)));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiEnvelope<object>>> ChangePassword(ChangePasswordRequest request)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId)) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new ProblemDetails { Title = "Complete la contraseña actual y la nueva." });
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsEnabled) return Unauthorized();
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["newPassword"] = result.Errors.Select(x => x.Description).ToArray()
            })
            {
                Title = "No fue posible cambiar la contraseña."
            });
        }
        user.LastPasswordChangedAtUtc = DateTimeOffset.UtcNow;
        user.MustChangePassword = false;
        await userManager.UpdateAsync(user);
        await signInManager.RefreshSignInAsync(user);
        return Ok(new ApiEnvelope<object>(new { changed = true }, "Contraseña actualizada."));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiEnvelope<object>>> Logout()
    {
        await signInManager.SignOutAsync();
        return Ok(new ApiEnvelope<object>(new { signedOut = true }));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiEnvelope<UserSessionDto>>> Me()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var userId)) return Unauthorized();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsEnabled) return Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        return Ok(new ApiEnvelope<UserSessionDto>(new UserSessionDto(user.Email!, user.DisplayName, roles.ToArray(), user.MustChangePassword)));
    }
}

public sealed record LoginRequest(
    [param: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress] string Email,
    [param: System.ComponentModel.DataAnnotations.Required] string Password);

public sealed record UserSessionDto(string Email, string DisplayName, IReadOnlyCollection<string> Roles, bool MustChangePassword);

