namespace Lidstroem.Core.Interfaces;

public interface INotificationService
{
    Task SendEmailAsync(
        string toAddress,
        string templateKey,
        object model,
        Guid tenantId,
        CancellationToken ct = default);

    Task SendInAppAsync(
        int actorId,
        string title,
        string message,
        string? actionUrl,
        Guid tenantId,
        CancellationToken ct = default);
}

public interface IEmailTemplateProvider
{
    Task<EmailTemplate?> GetTemplateAsync(string templateKey, Guid tenantId);
}

public record EmailTemplate(string Subject, string HtmlBody, string? PlainTextBody = null);

public interface IStorageProvider
{
    Task<string> StoreAsync(Stream content, string fileName, string contentType);
    Task<Stream> RetrieveAsync(string storagePath);
    Task DeleteAsync(string storagePath);
    Task<string?> GetPublicUrlAsync(string storagePath);
}

public interface IAclService
{
    Task GrantAsync(AclEntry entry);
    Task RevokeAsync(int resourceId, string resourceType, int grantedToActorId, AclAction action);
    Task<bool> HasAccessAsync(int actorId, int resourceId, string resourceType, AclAction action);
    Task<IReadOnlyList<AclEntry>> GetGrantsAsync(int resourceId, string resourceType);
    Task<IReadOnlyList<AclEntry>> GetActorGrantsAsync(int actorId);
    // FIX #4: Ownership check — returns true if actorId is the original granter on this resource
    Task<bool> IsGranterAsync(int actorId, int resourceId, string resourceType);
}

public enum AclAction { Read = 1, Write = 2, Delete = 3, Share = 4 }

public record AclEntry(
    int ResourceId,
    string ResourceType,
    int GrantedByActorId,
    int GrantedToActorId,
    AclAction Action,
    DateTime? ExpiresAt = null);
