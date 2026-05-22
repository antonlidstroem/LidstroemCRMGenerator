using Lidstroem.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;

namespace Lidstroem.Plugins.Reports;

public static class ReportsExtensions
{
    /// <summary>
    /// Call from Program.cs: builder.Services.AddReports();
    /// Scans all Lidstroem assemblies for IReportProvider implementations.
    /// </summary>
    public static IServiceCollection AddReports(this IServiceCollection services)
    {
        services.Scan(scan => scan
            .FromApplicationDependencies(a => a.FullName?.StartsWith("Lidstroem") == true)
            .AddClasses(classes => classes.AssignableTo<IReportProvider>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddScoped<QueryEngine>();

        return services;
    }
}
