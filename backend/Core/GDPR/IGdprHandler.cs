using MediatR;

namespace Lidstroem.Core.GDPR;

/// <summary>
/// Implemented by any plugin that holds Actor-related data and must
/// participate in GDPR forget operations.
/// </summary>
public interface IGdprHandler
{
    string HandlerName { get; }
    Task<GdprHandlerResult> HandleForgetAsync(
        int subjectId,
        string subjectType,
        Guid tenantId,
        CancellationToken ct = default);
}

public record GdprHandlerResult(
    string HandlerName,
    bool Success,
    int RecordsAffected,
    string? ErrorMessage = null)
{
    public static GdprHandlerResult Ok(string name, int records) => new(name, true, records);
    public static GdprHandlerResult Failed(string name, string error) => new(name, false, 0, error);
    public static GdprHandlerResult Skipped(string name) => new(name, true, 0);
}

public class ForgetSubjectCommand : IRequest<GdprResult>
{
    public int SubjectId { get; init; }
    public string SubjectType { get; init; } = string.Empty;
    public Guid TenantId { get; init; }
    public string? Email { get; init; }
    public int? RequestedByActorId { get; init; }
}

public class GdprResult
{
    public bool AllSucceeded => Results.All(r => r.Success);
    public IReadOnlyList<GdprHandlerResult> Results { get; init; } = new List<GdprHandlerResult>();
    public DateTime ExecutedAt { get; init; } = DateTime.UtcNow;
    public IEnumerable<GdprHandlerResult> Failed => Results.Where(r => !r.Success);
}
