using Lidstroem.Core.Events;
using Lidstroem.Core.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Plugins.Communication.Handlers;

/// <summary>
/// Sends a welcome notification when a new Actor is created.
/// Listens to the Core event — no reference to Persons plugin.
/// </summary>
public class ActorCreatedNotificationHandler : INotificationHandler<ActorCreatedEvent>
{
    private readonly INotificationService _notifications;
    private readonly ILogger<ActorCreatedNotificationHandler> _logger;

    public ActorCreatedNotificationHandler(
        INotificationService notifications,
        ILogger<ActorCreatedNotificationHandler> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(ActorCreatedEvent notification, CancellationToken cancellationToken)
    {
        await _notifications.SendInAppAsync(
            notification.ActorId,
            "Welcome!",
            "Your account has been created.",
            null,
            notification.TenantId,
            cancellationToken);
    }
}

/// <summary>
/// Sends a GDPR confirmation email after an Actor has been forgotten.
/// Listens to the Core event — no reference to GDPR plugin.
/// </summary>
public class ActorForgottenEmailHandler : INotificationHandler<ActorForgottenEvent>
{
    private readonly INotificationService _notifications;
    private readonly ILogger<ActorForgottenEmailHandler> _logger;

    public ActorForgottenEmailHandler(
        INotificationService notifications,
        ILogger<ActorForgottenEmailHandler> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(ActorForgottenEvent notification, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(notification.Email))
        {
            _logger.LogWarning("[GDPR] ActorForgottenEvent has no email address — skipping confirmation.");
            return;
        }

        await _notifications.SendEmailAsync(
            notification.Email,
            "Actor.Forgotten",
            new { notification.AllSucceeded },
            notification.TenantId,
            cancellationToken);
    }
}
