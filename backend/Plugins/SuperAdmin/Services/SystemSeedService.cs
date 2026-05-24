using Lidstroem.Core.Auth;
using Lidstroem.Core.Constants;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.Data;
using Lidstroem.Infrastructure.RBAC.Entities;
using Lidstroem.Plugins.SuperAdmin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Plugins.SuperAdmin.Services;

public class SystemSeedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<SystemSeedService> _logger;

    public SystemSeedService(
        IServiceProvider services,
        IHostEnvironment env,
        IConfiguration config,
        ILogger<SystemSeedService> logger)
    {
        _services = services;
        _env = env;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await SeedDevTenantAsync(context, cancellationToken);
        await SeedAnonymousActorAsync(context, cancellationToken);
        await SeedSuperAdminAsync(context, hasher, cancellationToken);
    }

    private async Task SeedDevTenantAsync(AppDbContext context, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return;

        var exists = await context.Set<Tenant>().IgnoreQueryFilters()
            .AnyAsync(t => t.ExternalId == TenantConstants.DefaultDevTenantId, ct);

        if (exists) return;

        context.Set<Tenant>().Add(new Tenant
        {
            ExternalId = TenantConstants.DefaultDevTenantId,
            Name = "Development Tenant",
            IsActive = true,
            ActivatedAt = DateTime.UtcNow,
            TenantId = TenantConstants.SystemTenantId
        });

        await context.SaveChangesAsync(ct);
        _logger.LogInformation("[Seed] Dev tenant created");
    }

    private async Task SeedAnonymousActorAsync(AppDbContext context, CancellationToken ct)
    {
        var exists = await context.Set<Actor>().IgnoreQueryFilters()
            .AnyAsync(a => a.Id == ActorConstants.AnonymousActorId, ct);

        if (exists) return;

        // FIX #6: ExecuteSqlInterpolatedAsync safely parameterises values.
        // ExecuteSqlRawAsync with $"..." does NOT parameterise — never use it with variables.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SET IDENTITY_INSERT Actor ON; INSERT INTO Actor (Id, DisplayName, Email, TenantId, CreatedAt) VALUES ({ActorConstants.AnonymousActorId}, N'[anonymous]', N'anonymous@gdpr.invalid', {TenantConstants.SystemTenantId}, GETUTCDATE()); SET IDENTITY_INSERT Actor OFF;",
            ct);

        _logger.LogInformation("[Seed] Anonymous actor created");
    }

    private async Task SeedSuperAdminAsync(
        AppDbContext context, IPasswordHasher hasher, CancellationToken ct)
    {
        var adminEmail = _config["SuperAdmin:Email"] ?? "admin@lidstroem.dev";

        string adminPassword;
        if (_env.IsDevelopment())
        {
            adminPassword = _config["SuperAdmin:Password"] ?? "ChangeMe123!";
        }
        else
        {
            adminPassword = _config["SuperAdmin:Password"]
                ?? throw new InvalidOperationException(
                    "SuperAdmin:Password must be set in configuration for non-Development environments.");

            if (adminPassword.Length < 16)
                throw new InvalidOperationException(
                    "SuperAdmin:Password must be at least 16 characters.");
        }

        var exists = await context.Set<ActorCredentials>().IgnoreQueryFilters()
            .AnyAsync(c => c.Identifier == adminEmail, ct);

        if (exists) return;

        // Wrap everything in a transaction so a mid-seed crash doesn't leave
        // an Actor without credentials or a Role without permissions.
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var actor = new Actor
            {
                DisplayName = "Super Admin",
                Email = adminEmail,
                TenantId = TenantConstants.SystemTenantId
            };

            context.Set<Actor>().Add(actor);
            await context.SaveChangesAsync(ct);

            context.Set<ActorCredentials>().Add(new ActorCredentials
            {
                ActorId = actor.Id,
                Identifier = adminEmail,
                PasswordHash = hasher.Hash(adminPassword),
                TenantId = TenantConstants.SystemTenantId
            });

            await context.SaveChangesAsync(ct);

            var permissions = await context.Set<Permission>().IgnoreQueryFilters()
                .Where(p => p.IsActive).ToListAsync(ct);

            var role = new Role
            {
                Name = "SuperAdmin",
                DisplayName = "Super Administrator",
                IsSystemRole = true,
                TenantId = TenantConstants.SystemTenantId,
                RolePermissions = permissions
                    .Select(p => new RolePermission { Permission = p })
                    .ToList()
            };

            context.Set<Role>().Add(role);
            await context.SaveChangesAsync(ct);

            context.Set<ActorRoleAssignment>().Add(new ActorRoleAssignment
            {
                ActorId = actor.Id,
                RoleId = role.Id,
                TenantId = TenantConstants.SystemTenantId
            });

            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation("[Seed] SuperAdmin created: {Email}", adminEmail);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
