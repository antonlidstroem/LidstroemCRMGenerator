using System.Security.Cryptography;
using Lidstroem.Core.Auth;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Interfaces;
using Lidstroem.Plugins.Invitations.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.Invitations.Services;

public class InvitationService
{
    private readonly DbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly INotificationService _notifications;
    private readonly IPermissionService _permissions;

    public InvitationService(
        DbContext context,
        IPasswordHasher hasher,
        INotificationService notifications,
        IPermissionService permissions)
    {
        _context = context;
        _hasher = hasher;
        _notifications = notifications;
        _permissions = permissions;
    }

    public async Task<Invitation> CreateAsync(
        string email, Guid tenantId, int invitedByActorId,
        string? roleName, string baseUrl)
    {
        var existingCredentials = await _context.Set<ActorCredentials>()
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Identifier == email);

        if (existingCredentials)
            throw new InvalidOperationException($"Email '{email}' is already registered.");

        var pending = await _context.Set<Invitation>()
            .Where(i => i.Email == email
                     && i.TenantId == tenantId
                     && i.Status == InvitationStatus.Pending)
            .ToListAsync();

        foreach (var old in pending)
            old.Status = InvitationStatus.Revoked;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');

        var invitation = new Invitation
        {
            Email = email,
            Token = token,
            TenantId = tenantId,
            InvitedByActorId = invitedByActorId,
            RoleName = roleName,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _context.Set<Invitation>().Add(invitation);
        await _context.SaveChangesAsync();

        var acceptUrl = $"{baseUrl.TrimEnd('/')}/accept-invitation?token={token}";
        await _notifications.SendEmailAsync(
            email, "Invitation.Welcome",
            new { AcceptUrl = acceptUrl, ExpiresAt = invitation.ExpiresAt },
            tenantId);

        return invitation;
    }

    public async Task<Actor> AcceptAsync(
        string token, string displayName, string password)
    {
        var invitation = await _context.Set<Invitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Token == token)
            ?? throw new InvalidOperationException("Invitation token not found.");

        if (!invitation.IsUsable)
            throw new InvalidOperationException(
                invitation.IsExpired ? "Invitation has expired." : "Invitation is no longer valid.");

        var actor = new Actor
        {
            DisplayName = displayName,
            Email = invitation.Email,
            TenantId = invitation.TenantId
        };

        _context.Set<Actor>().Add(actor);
        await _context.SaveChangesAsync();

        _context.Set<ActorCredentials>().Add(new ActorCredentials
        {
            ActorId = actor.Id,
            Identifier = invitation.Email,
            PasswordHash = _hasher.Hash(password),
            TenantId = invitation.TenantId
        });

        // BUG FIX #10: The original code added ActorCredentials but did NOT call
        // SaveChangesAsync before AssignRoleAsync. If AssignRoleAsync threw (e.g. the role
        // didn't exist), the actor would be in the DB but without credentials — permanently
        // locked out. Save credentials first so the actor is always in a usable state.
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(invitation.RoleName))
        {
            try
            {
                await _permissions.AssignRoleAsync(actor.Id, invitation.TenantId, invitation.RoleName);
            }
            catch (InvalidOperationException)
            {
                // Role may not exist yet — non-fatal
            }
        }

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedByActorId = actor.Id;
        invitation.AcceptedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return actor;
    }
}
