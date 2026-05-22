using System.Text;
using Lidstroem.Core.Interfaces;
using Lidstroem.Infrastructure.Auth;
using Lidstroem.Infrastructure.Data;
using Lidstroem.Infrastructure.Interceptors;
using Lidstroem.Infrastructure.RBAC.Services;
using Lidstroem.Infrastructure.Realtime;
using Lidstroem.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Scrutor;

namespace Lidstroem.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var jwtSecret = configuration["Auth:Jwt:Secret"]
            ?? throw new InvalidOperationException("Auth:Jwt:Secret is not configured.");

        // HTTP context
        services.AddHttpContextAccessor();

        // ── JWT authentication ────────────────────────────────────────────────
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Auth:Jwt:Issuer"] ?? "lidstroem",
                    ValidAudience = configuration["Auth:Jwt:Audience"] ?? "lidstroem",
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.Zero
                };

                // Allow SignalR to read the token from the query string.
                // Browsers can't set custom headers on WebSocket upgrade requests —
                // this is the standard workaround.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments("/hubs/lidstroem"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        // ── Auth implementations ──────────────────────────────────────────────
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IAuthProvider, JwtAuthProvider>();
        services.AddScoped<ITenantContext, JwtTenantContext>();
        services.AddScoped<ICurrentUserContext, JwtCurrentUserContext>();

        // ── RBAC ──────────────────────────────────────────────────────────────
        services.AddMemoryCache();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddHostedService<PermissionRegistry>();

        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<IPermissionProvider>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        // ── Plugin scanning ───────────────────────────────────────────────────
        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<IEntityExtensionProvider>()
                .Where(t => t.Name != "GenericResourceExtensionProvider"))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<ILinkResolver>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddScoped<ILinkResolverService, LinkResolverService>();

        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<IPluginMetadata>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        // Scan for ICustomPageMetadata implementations from all custom page plugins
        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<ICustomPageMetadata>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());

        // ── EF interceptors ───────────────────────────────────────────────────
        services.AddScoped<AuditingInterceptor>();
        services.AddScoped<DomainEventInterceptor>();

        // ── DbContext ─────────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString)
                   .AddInterceptors(
                       sp.GetRequiredService<AuditingInterceptor>(),
                       sp.GetRequiredService<DomainEventInterceptor>());
        });

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ── SignalR + realtime notifier ────────────────────────────────────────
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false; // set true in Development if needed
            options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB — plenty for our messages
        });

        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

        return services;
    }

    /// <summary>
    /// Maps the SignalR hub endpoint. Call after app.UseAuthorization() in Program.cs.
    /// </summary>
    public static WebApplication MapLidstroemHubs(this WebApplication app)
    {
        app.MapHub<LidstroemHub>("/hubs/lidstroem");
        return app;
    }
}
