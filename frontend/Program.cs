// frontend/Program.cs

using Lidstroem.Frontend.Core.Auth;
using Lidstroem.Frontend.Core.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// ── Custom page registration ──────────────────────────────────────────────────
// CustomPageRegistry.Register("ProjectDashboard",
//     typeof(Lidstroem.CustomPages.ProjectDashboard.ProjectDashboardPage));
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Lidstroem.Frontend.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"]
    ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });

builder.Services.AddScoped<AppState>();
builder.Services.AddScoped<RealtimeService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<SchemaService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<SkinService>();
builder.Services.AddScoped<ReportService>();       // ← ny
builder.Services.AddSingleton<CustomPageRegistry>();

await builder.Build().RunAsync();
