using System.Security.Cryptography;
using System.Text;
using BCMetals.NasAudit.Domain.Entities;
using BCMetals.NasAudit.Domain.Enums;
using BCMetals.NasAudit.Infrastructure.Identity;
using BCMetals.NasAudit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BCMetals.NasAudit.Infrastructure.Seeding;

public sealed class DatabaseSeeder(
    AuditDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        foreach (var role in new[] { RoleNames.Administrator, RoleNames.Auditor })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                EnsureSucceeded(result, $"crear rol {role}");
            }
        }

        await SeedInitialAdminAsync();
        if (configuration.GetValue<bool>("Demo:Enabled"))
        {
            await SeedDemoAsync(cancellationToken);
        }
    }

    private async Task SeedInitialAdminAsync()
    {
        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        if (administrators.Count > 0) return;

        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("No existe un administrador. Configure Bootstrap:AdminEmail y Bootstrap:AdminPassword para crear el primero.");
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Administrador TI", EmailConfirmed = true, MustChangePassword = true };
            EnsureSucceeded(await userManager.CreateAsync(user, password), "crear administrador inicial");
        }

        if (!await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, RoleNames.Administrator), "asignar rol administrador");
        }
    }

    private async Task SeedDemoAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.AuditEvents.AnyAsync(x => x.DataMode == DataMode.Demo, cancellationToken)) return;
        var source = new EventSource { Name = "synthetic-demo-v1", Kind = "Synthetic" };
        dbContext.EventSources.Add(source);

        var users = new[] { "soporte", "operaciones", "calidad", "administracion" };
        var shares = new[] { "BCMETALS", "Bodega", "PRUEBA_AUDITORIA_SMB" };
        var actions = new[] { AuditAction.Creation, AuditAction.Open, AuditAction.Write, AuditAction.Deletion, AuditAction.Rename, AuditAction.Connection };
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 72; index++)
        {
            var action = actions[index % actions.Length];
            var path = $"/volume1/{shares[index % shares.Length]}/DEMO/documento-{index % 15:00}.pdf";
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"demo:{index}"))).ToLowerInvariant();
            dbContext.AuditEvents.Add(new AuditEvent
            {
                EventTimestampUtc = now.AddHours(-index * 3),
                SmbUser = users[index % users.Length],
                ClientIp = $"client-{index % 10}.example.invalid",
                Operation = OperationOf(action),
                InterpretedAction = action,
                InterpretationConfidence = InterpretationConfidence.Confirmed,
                OperationResult = "ok",
                ShareName = shares[index % shares.Length],
                FilePath = path,
                FileExtension = ".pdf",
                RawMessage = $"SYNTHETIC_DEMO|{index}|{path}",
                EventSource = source,
                DeduplicationKey = key,
                ParserVersion = "synthetic-v1",
                DataMode = DataMode.Demo,
                MetadataJson = "{\"synthetic\":true}"
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string OperationOf(AuditAction action) => action switch
    {
        AuditAction.Creation => "create_file",
        AuditAction.Open => "create_file",
        AuditAction.Write => "pwrite",
        AuditAction.Deletion => "unlinkat",
        AuditAction.Rename => "renameat",
        AuditAction.Connection => "connect",
        _ => "unknown"
    };

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException($"No fue posible {operation}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
    }
}

