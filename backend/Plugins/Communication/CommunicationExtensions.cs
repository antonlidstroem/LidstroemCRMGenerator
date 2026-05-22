using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Communication.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lidstroem.Plugins.Communication;

public static class CommunicationExtensions
{
    public static IServiceCollection AddCommunication(this IServiceCollection services)
    {
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IEmailTemplateProvider, DbEmailTemplateProvider>();
        services.AddScoped<ScribanTemplateRenderer>();
        services.AddScoped<SmtpEmailSender>();
        return services;
    }
}
