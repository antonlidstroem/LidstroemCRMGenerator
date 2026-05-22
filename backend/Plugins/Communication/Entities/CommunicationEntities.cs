using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.Communication.Entities;

public class Notification : BaseEntity
{
    public int ActorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class NotificationTemplate : BaseEntity
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? PlainTextBody { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EmailLog : BaseEntity
{
    public string ToAddress { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public EmailStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public enum EmailStatus { Sent = 1, Failed = 2, Queued = 3 }
