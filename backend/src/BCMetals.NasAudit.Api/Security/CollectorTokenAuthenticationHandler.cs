using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BCMetals.NasAudit.Api.Security;

public static class CollectorAuthenticationDefaults
{
    public const string Scheme = "CollectorToken";
    public const string TokenHeader = "X-Collector-Token";
    public const string CollectorIdHeader = "X-Collector-Id";
}

public sealed class CollectorTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = configuration["Collector:Token"];
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32)
            return Task.FromResult(AuthenticateResult.Fail("La credencial del recolector no está configurada."));
        if (!Request.Headers.TryGetValue(CollectorAuthenticationDefaults.TokenHeader, out var supplied) ||
            !Request.Headers.TryGetValue(CollectorAuthenticationDefaults.CollectorIdHeader, out var collectorId) ||
            string.IsNullOrWhiteSpace(collectorId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied.ToString());
        if (expectedBytes.Length != suppliedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
            return Task.FromResult(AuthenticateResult.Fail("Credencial del recolector inválida."));

        var identity = new ClaimsIdentity(
            [new Claim("collector_id", collectorId.ToString())],
            CollectorAuthenticationDefaults.Scheme);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), CollectorAuthenticationDefaults.Scheme)));
    }
}
