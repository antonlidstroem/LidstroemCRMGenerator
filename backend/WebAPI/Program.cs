using System.Reflection;
using System.Threading.RateLimiting;
using Lidstroem.Infrastructure;
using Lidstroem.Infrastructure.Realtime;
using Lidstroem.Plugins.Communication;
using Lidstroem.Plugins.GDPR;
using Lidstroem.Plugins.Invitations.Services;
using Lidstroem.Plugins.Resources;
using Lidstroem.Plugins.Schema;
using Lidstroem.Plugins.SuperAdmin;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.ACL.Services;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using Lidstroem.Plugins.Reports;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure (auth, EF, RBAC, SignalR, realtime notifier) ──────────────
builder.Services.AddInfrastructure(builder.Configuration);

// ── CORS — allow Blazor WASM frontend + WebSocket upgrade ────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["App:FrontendUrl"] ?? "https://localhost:5001",
                "http://localhost:5000",
                "https://localhost:5001",
                "http://localhost:7200"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR WebSocket handshake
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

// ── MediatR — scan all Lidstroem assemblies + register broadcast behaviour ───
var lidstroemAssemblies = AppDomain.CurrentDomain.GetAssemblies()
    .Where(a => a.GetName().Name?.StartsWith("Lidstroem") == true)
    .ToArray();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    foreach (var assembly in lidstroemAssemblies)
        cfg.RegisterServicesFromAssembly(assembly);

    // Automatically broadcast realtime events for any IRequest that also
    // implements IRealtimeBroadcast — plugins opt in, no boilerplate required.
    cfg.AddOpenBehavior(typeof(RealtimeBroadcastBehavior<,>));
});

// ── Plugin feature services ───────────────────────────────────────────────────
builder.Services.AddCommunication();
builder.Services.AddResources(builder.Environment);
builder.Services.AddSchemaRegistry();
builder.Services.AddGdpr();
builder.Services.AddSuperAdmin();
builder.Services.AddReports();
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

// ── SignalR hub ───────────────────────────────────────────────────────────────
app.MapLidstroemHubs();

app.Run();

public partial class Program { }
