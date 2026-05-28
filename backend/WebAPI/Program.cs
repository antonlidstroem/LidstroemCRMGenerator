using System.Reflection;
using System.Threading.RateLimiting;
using Lidstroem.Infrastructure;
using Lidstroem.Plugins.Communication;
using Lidstroem.Plugins.GDPR;
using Lidstroem.Plugins.Invitations.Services;
using Lidstroem.Plugins.Resources;
using Lidstroem.Plugins.Schema;
using Lidstroem.Plugins.SuperAdmin;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.ACL.Services;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure (auth, EF, RBAC, interceptors, link resolvers, etc.) ──────
builder.Services.AddInfrastructure(builder.Configuration);

// ── CORS ──────────────────────────────────────────────────────────────────────
// In Development: read AllowedCorsOrigins from appsettings.Development.json.
// This avoids having to update Program.cs every time the dev frontend port changes
// (Blazor WASM dev server picks a random port on each machine/run).
// In Production: set App:FrontendUrl to your deployed frontend URL.

var allowedOrigins = builder.Environment.IsDevelopment()
    ? builder.Configuration.GetSection("AllowedCorsOrigins").Get<string[]>()
      ?? new[] { builder.Configuration["App:FrontendUrl"] ?? "https://localhost:5001" }
    : new[] { builder.Configuration["App:FrontendUrl"]
              ?? throw new InvalidOperationException("App:FrontendUrl must be set in production.") };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Blazor WASM dev server picks a random port on each run.
            // Allow any localhost/127.0.0.1 origin in development so CORS
            // never blocks requests regardless of which port is assigned.
            policy.SetIsOriginAllowed(origin =>
                {
                    var uri = new Uri(origin);
                    return uri.Host is "localhost" or "127.0.0.1";
                })
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .WithExposedHeaders("X-Total-Count");
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .WithExposedHeaders("X-Total-Count");
        }
    });
});

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { Error = "Too many requests. Please wait before trying again." },
            cancellationToken);
    };
});

// ── MediatR — scan all Lidstroem assemblies ───────────────────────────────────
var lidstroemAssemblies = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("Lidstroem") == true)
    .ToArray();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    foreach (var assembly in lidstroemAssemblies)
        cfg.RegisterServicesFromAssembly(assembly);
});

// ── Plugin feature services ───────────────────────────────────────────────────
builder.Services.AddCommunication();
builder.Services.AddResources(builder.Environment);
builder.Services.AddSchemaRegistry();
builder.Services.AddGdpr();
builder.Services.AddSuperAdmin();

builder.Services.AddScoped<IAclService, Lidstroem.Plugins.ACL.Services.AclService>();
builder.Services.AddScoped<InvitationService>();

// ── MVC — discover controllers from all plugin assemblies ─────────────────────
var pluginAssemblies = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("Lidstroem.Plugins") == true)
    .ToList();

var mvcBuilder = builder.Services.AddControllers();
foreach (var assembly in pluginAssemblies)
    mvcBuilder.AddApplicationPart(assembly);

mvcBuilder.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler =
        System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("FrontendPolicy");
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSuperAdmin();
app.MapControllers();
app.MapLidstroemHubs();
app.Run();

public partial class Program { }