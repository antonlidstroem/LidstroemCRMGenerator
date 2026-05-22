using Lidstroem.Core.Entities.Base;

namespace Lidstroem.Plugins.GDPR.Entities;

public class GdprLog : BaseEntity
{
    public int ForgottenSubjectId { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public string? EmailHash { get; set; }
    public int? RequestedByActorId { get; set; }
    public bool AllHandlersSucceeded { get; set; }
    public int HandlersRun { get; set; }
    public int HandlersFailed { get; set; }
    public string ResultJson { get; set; } = string.Empty;
}
