using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lidstroem.Core.Events;
using Lidstroem.Core.GDPR;
using Lidstroem.Plugins.GDPR.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lidstroem.Plugins.GDPR;

public class GdprOrchestrator : IRequestHandler<ForgetSubjectCommand, GdprResult>
{
    private readonly IEnumerable<IGdprHandler> _handlers;
    private readonly DbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<GdprOrchestrator> _logger;

    public GdprOrchestrator(
        IEnumerable<IGdprHandler> handlers,
        DbContext context,
        IPublisher publisher,
        ILogger<GdprOrchestrator> logger)
    {
        _handlers = handlers;
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<GdprResult> Handle(
        ForgetSubjectCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[GDPR] Starting forget operation for SubjectId={Id} SubjectType={Type}",
            command.SubjectId, command.SubjectType);

        var results = new List<GdprHandlerResult>();

        foreach (var handler in _handlers)
        {
            try
            {
                var result = await handler.HandleForgetAsync(
                    command.SubjectId, command.SubjectType,
                    command.TenantId, cancellationToken);

                results.Add(result);

                if (result.Success)
                    _logger.LogInformation("[GDPR] {Handler}: {Records} records affected",
                        result.HandlerName, result.RecordsAffected);
                else
                    _logger.LogError("[GDPR] {Handler} failed: {Error}",
                        result.HandlerName, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                results.Add(GdprHandlerResult.Failed(handler.HandlerName, ex.Message));
                _logger.LogError(ex, "[GDPR] {Handler} threw an exception", handler.HandlerName);
            }
        }

        var gdprResult = new GdprResult
        {
            Results = results.AsReadOnly(),
            ExecutedAt = DateTime.UtcNow
        };

        await WriteAuditLogAsync(command, gdprResult, cancellationToken);

        if (string.Equals(command.SubjectType, "Actor", StringComparison.OrdinalIgnoreCase))
        {
            await _publisher.Publish(new ActorForgottenEvent(
                command.SubjectId,
                command.TenantId,
                command.Email,
                gdprResult.AllSucceeded), cancellationToken);
        }

        return gdprResult;
    }

    private async Task WriteAuditLogAsync(
        ForgetSubjectCommand command, GdprResult result, CancellationToken ct)
    {
        var emailHash = command.Email != null
            ? Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(command.Email.ToLowerInvariant())))
            : null;

        var log = new GdprLog
        {
            ForgottenSubjectId = command.SubjectId,
            SubjectType = command.SubjectType,
            TenantId = command.TenantId,
            EmailHash = emailHash,
            RequestedByActorId = command.RequestedByActorId,
            AllHandlersSucceeded = result.AllSucceeded,
            HandlersRun = result.Results.Count,
            HandlersFailed = result.Failed.Count(),
            ResultJson = JsonSerializer.Serialize(result)
        };

        _context.Set<GdprLog>().Add(log);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[GDPR] Failed to write audit log!");
        }
    }
}
