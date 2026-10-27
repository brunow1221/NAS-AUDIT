using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BCMetals.NasAudit.Api.Infrastructure;
using BCMetals.NasAudit.Api.Security;
using BCMetals.NasAudit.Infrastructure;
using BCMetals.NasAudit.Infrastructure.Identity;
using BCMetals.NasAudit.Infrastructure.Seeding;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 25 * 1024 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(AppContext.BaseDirectory, "keys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("BCMETALS.NAS.Audit");
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication()
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, CollectorTokenAuthenticationHandler>(
        CollectorAuthenticationDefaults.Scheme, _ => { });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "bcmetals_xsrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Configuration.GetValue<bool>("App:CookieSecure")
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.HeaderName = "X-XSRF-TOKEN";
});
builder.Services.AddControllersWithViews(options => options.Filters.Add<Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));
    options.AddPolicy("ingestion", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Request.Headers[CollectorAuthenticationDefaults.CollectorIdHeader].ToString() is { Length: > 0 } collectorId
            ? collectorId
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("analytics", context => RateLimitPartition.GetConcurrencyLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = 4,
            QueueLimit = 8,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    var network = builder.Configuration["ForwardedHeaders:KnownNetwork"]
        ?? throw new InvalidOperationException("ForwardedHeaders:KnownNetwork must be configured.");
    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    options.ForwardLimit = 1;
});

var app = builder.Build();

app.UseForwardedHeaders();
if (app.Environment.IsProduction()) app.UseHsts();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    if (context.Request.Path.StartsWithSegments("/api"))
        context.Response.Headers.TryAdd("Cache-Control", "no-store");
    await next();
});
app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var isProtectedPortalRequest = context.User.Identity?.IsAuthenticated == true &&
        context.Request.Path.StartsWithSegments("/api/v1") &&
        !context.Request.Path.StartsWithSegments("/api/v1/auth") &&
        !context.Request.Path.Equals("/api/v1/system/status", StringComparison.OrdinalIgnoreCase);
    if (isProtectedPortalRequest)
    {
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var userId))
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null || !user.IsEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (user.MustChangePassword)
            {
                await Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Cambio de contraseña requerido",
                    detail: "Actualice su contraseña antes de utilizar las funciones del portal.")
                    .ExecuteAsync(context);
                return;
            }
        }
    }
    await next();
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapOpenApi().RequireAuthorization();
app.MapControllers();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(CancellationToken.None);
}

await app.RunAsync();

public partial class Program;

