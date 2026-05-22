// BUG FIX #19: Replaced System.Net.Mail.SmtpClient (marked [Obsolete] since .NET 5,
// lacks modern TLS support and has no proper async design) with MailKit's SmtpClient,
// which supports STARTTLS, OAuth2, SMTPS, and is fully async-first.
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Lidstroem.Core.Constants;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Communication.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Runtime;

namespace Lidstroem.Plugins.Communication.Services;

public class NotificationService : INotificationService
{
    private readonly DbContext _context;
    private readonly IEmailTemplateProvider _templateProvider;
    private readonly ScribanTemplateRenderer _renderer;
    private readonly SmtpEmailSender _smtp;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DbContext context,
        IEmailTemplateProvider templateProvider,
        ScribanTemplateRenderer renderer,
        SmtpEmailSender smtp,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _templateProvider = templateProvider;
        _renderer = renderer;
        _smtp = smtp;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toAddress, string templateKey, object model,
        Guid tenantId, CancellationToken ct = default)
    {
        var template = await _templateProvider.GetTemplateAsync(templateKey, tenantId);
        if (template == null)
        {
            _logger.LogWarning("[Communication] No template found: {Key}", templateKey);
            return;
        }

        var subject = _renderer.Render(template.Subject, model);
        var htmlBody = _renderer.Render(template.HtmlBody, model);
        var log = new EmailLog
        {
            ToAddress = toAddress,
            TemplateKey = templateKey,
            Subject = subject,
            TenantId = tenantId
        };

        try
        {
            await _smtp.SendAsync(toAddress, subject, htmlBody);
            log.Status = EmailStatus.Sent;
        }
        catch (Exception ex)
        {
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "[Communication] Failed to send to {To}", toAddress);
        }
        finally
        {
            _context.Set<EmailLog>().Add(log);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task SendInAppAsync(
        int actorId, string title, string message,
        string? actionUrl, Guid tenantId, CancellationToken ct = default)
    {
        _context.Set<Notification>().Add(new Notification
        {
            ActorId = actorId,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            TenantId = tenantId
        });
        await _context.SaveChangesAsync(ct);
    }
}

public class DbEmailTemplateProvider : IEmailTemplateProvider
{
    private readonly DbContext _context;

    public DbEmailTemplateProvider(DbContext context) => _context = context;

    public async Task<EmailTemplate?> GetTemplateAsync(string templateKey, Guid tenantId)
    {
        var candidates = await _context.Set<NotificationTemplate>().IgnoreQueryFilters()
            .Where(t => t.TemplateKey == templateKey && t.IsActive
                     && (t.TenantId == tenantId || t.TenantId == TenantConstants.SystemTenantId))
            .ToListAsync();

        var template = candidates.FirstOrDefault(t => t.TenantId == tenantId)
                    ?? candidates.FirstOrDefault(t => t.TenantId == TenantConstants.SystemTenantId);

        if (template != null)
            return new EmailTemplate(template.Subject, template.HtmlBody, template.PlainTextBody);

        return GetHardcodedFallback(templateKey);
    }

    private static EmailTemplate? GetHardcodedFallback(string templateKey) => templateKey switch
    {
        "Actor.Welcome"             => new EmailTemplate("Welcome!", "<p>Hi {{display_name}}, your account has been created!</p>"),
        "Actor.Forgotten"           => new EmailTemplate("Your data has been deleted", "<p>Your account and associated data have been deleted.</p>"),
        "Invitation.Welcome"        => new EmailTemplate("You have been invited!", "<p>Click here to accept: <a href='{{accept_url}}'>Accept</a></p>"),
        _ => null
    };
}

public class ScribanTemplateRenderer
{
    public string Render(string templateSource, object model)
    {
        var template = Template.Parse(templateSource);
        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Template errors: {string.Join(", ", template.Messages)}");

        var scriptObject = new ScriptObject();
        scriptObject.Import(model, renamer: member =>
            string.Concat(member.Name.Select((c, i) =>
                i > 0 && char.IsUpper(c)
                    ? "_" + char.ToLower(c)
                    : char.ToLower(c).ToString())));

        var context = new TemplateContext();
        context.PushGlobal(scriptObject);
        return template.Render(context);
    }
}

public class SmtpEmailSender
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    // BUG FIX #19 (continued): Full MailKit implementation.
    // MailKit correctly negotiates STARTTLS / SMTPS and works with modern mail providers
    // (Gmail, SendGrid, Office 365) without the TLS handshake failures seen with SmtpClient.
    public async Task SendAsync(string toAddress, string subject, string htmlBody)
    {
        var host        = _config["Communication:Smtp:Host"]        ?? "localhost";
        var port        = int.Parse(_config["Communication:Smtp:Port"] ?? "587");
        var fromAddress = _config["Communication:Smtp:FromAddress"] ?? "noreply@lidstroem.dev";
        var fromName    = _config["Communication:Smtp:FromName"]    ?? "Lidstroem";
        var username    = _config["Communication:Smtp:Username"];
        var password    = _config["Communication:Smtp:Password"];
        // SecureSocketOptions: Auto lets MailKit negotiate the best available TLS.
        // Explicit override is possible via "Communication:Smtp:SecureSocketOptions" setting.
        var secureOptionRaw = _config["Communication:Smtp:SecureSocketOptions"] ?? "Auto";
        var secureOption = Enum.TryParse<SecureSocketOptions>(secureOptionRaw, out var opt)
            ? opt : SecureSocketOptions.Auto;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, secureOption);

        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            await client.AuthenticateAsync(username, password);

        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);

        _logger.LogInformation("[Email] Sent to {To}", toAddress);
    }
}
