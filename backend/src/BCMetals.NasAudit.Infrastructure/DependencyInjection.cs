using BCMetals.NasAudit.Application.Abstractions;
using BCMetals.NasAudit.Application.Alerts;
using BCMetals.NasAudit.Application.Parsing;
using BCMetals.NasAudit.Infrastructure.Identity;
using BCMetals.NasAudit.Infrastructure.Importing;
using BCMetals.NasAudit.Infrastructure.Ingestion;
using BCMetals.NasAudit.Infrastructure.Insights;
using BCMetals.NasAudit.Infrastructure.Operations;
using BCMetals.NasAudit.Infrastructure.Persistence;
using BCMetals.NasAudit.Infrastructure.Queries;
using BCMetals.NasAudit.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BCMetals.NasAudit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default no está configurada.");

        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(AuditDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "nas_audit");
                npgsql.CommandTimeout(30);
                npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
            }));

        services.AddOptions<RetentionOptions>()
            .Bind(configuration.GetSection(RetentionOptions.SectionName))
            .Validate(x => x.HistoricalDays is >= 30 and <= 36500, "Retention:HistoricalDays debe estar entre 30 y 36500.")
            .Validate(x => x.LiveDays is >= 30 and <= 36500, "Retention:LiveDays debe estar entre 30 y 36500.")
            .Validate(x => x.DemoDays is >= 1 and <= 36500, "Retention:DemoDays debe estar entre 1 y 36500.")
            .Validate(x => x.IngestionMetadataDays is >= 30 and <= 36500, "Retention:IngestionMetadataDays debe estar entre 30 y 36500.")
            .Validate(x => x.RejectedEventDays is >= 30 and <= 36500, "Retention:RejectedEventDays debe estar entre 30 y 36500.")
            .Validate(x => x.BatchSize is >= 100 and <= 10000, "Retention:BatchSize debe estar entre 100 y 10000.")
            .Validate(x => x.RunAtUtcHour is >= 0 and <= 23, "Retention:RunAtUtcHour debe estar entre 0 y 23.")
            .ValidateOnStart();
        services.AddOptions<MonitoringOptions>()
            .Bind(configuration.GetSection(MonitoringOptions.SectionName))
            .Validate(x => x.QueueDepthWarning >= 1, "Monitoring:QueueDepthWarning debe ser positivo.")
            .Validate(x => x.NoEventWarningHours >= 1, "Monitoring:NoEventWarningHours debe ser positivo.")
            .Validate(x => x.BehavioralWindowMinutes is >= 1 and <= 1440, "Monitoring:BehavioralWindowMinutes debe estar entre 1 y 1440.")
            .ValidateOnStart();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 14;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AuditDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.Name = "bcmetals_nas_audit";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = configuration.GetValue<bool>("App:CookieSecure")
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy("CanImport", policy => policy.RequireRole(RoleNames.Administrator));

        services.AddScoped<IAuditReadService, AuditReadService>();
        services.AddScoped<IImportService, CsvImportService>();
        services.AddScoped<ICollectorIngestionService, CollectorIngestionService>();
        services.AddScoped<IInsightsService, InsightsService>();
        services.AddScoped<IApplicationUserService, ApplicationUserService>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.AddScoped<IRetentionService, RetentionService>();
        services.AddScoped<IAlertCandidateEvaluator, AlertCandidateEvaluator>();
        services.AddSingleton<ILiveEventPublisher, LiveEventPublisher>();
        services.AddSingleton<IOperationalEventBuffer, OperationalEventBuffer>();
        services.AddSingleton<ISambaMessageParser, BasicSambaMessageParser>();
        services.AddScoped<DatabaseSeeder>();
        services.AddHostedService<RetentionBackgroundService>();
        return services;
    }
}

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string Auditor = "Auditor";
}

