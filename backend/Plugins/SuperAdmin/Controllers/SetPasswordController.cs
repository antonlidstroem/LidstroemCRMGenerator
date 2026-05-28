using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Lidstroem.Core.Auth;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Interfaces;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.SuperAdmin.Controllers;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class SetPasswordDto
{
    [Required]
    public int ActorId { get; set; }

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class GenerateResetTokenDto
{
    [Required]
    public int ActorId { get; set; }
}

public class ResetPasswordDto
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

// ── Controller ────────────────────────────────────────────────────────────────

/// <summary>
/// Two complementary flows for credential management:
///
/// Flow A — SuperAdmin sets password directly (no email needed):
///   POST /api/auth/set-password   { actorId, password }
///   Requires SuperAdmin.ManageCredentials. Used when creating a new user
///   or when an admin resets a password on behalf of a user.
///
/// Flow B — Token-based self-service reset (for users who know their email):
///   POST /api/auth/generate-reset-token   { actorId }   → { token, expiresAt }
///   POST /api/auth/reset-password         { token, password }   [AllowAnonymous]
///   The token is returned in the API response so the admin can share it
///   out-of-band (copy-paste, email, Slack). No email infrastructure required.
/// </summary>
[Route("api/auth")]
[ApiController]
public class SetPasswordController : ControllerBase
{
    private readonly DbContext _context;
    private readonly IPasswordHasher _hasher;

    // Tokens expire after 24 h — long enough for async onboarding flows.
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(24);

    public SetPasswordController(DbContext context, IPasswordHasher hasher)
    {
        _context = context;
        _hasher = hasher;
    }

    // ── Flow A: SuperAdmin sets a password directly ───────────────────────────

    // BUG-2 FIX: Originally [HttpPost] but called via ApiClient.PutAsync — 405 Method Not Allowed.
    // Accept both PUT (from admin UI) and POST (for future programmatic use).
    [HttpPut("set-password")]
    [HttpPost("set-password")]
    [Authorize]
    [RequirePermission("SuperAdmin.ManageCredentials")]
    public async Task<IActionResult> SetPassword(SetPasswordDto dto)
    {
        var actor = await _context.Set<Actor>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == dto.ActorId);

        if (actor == null) return NotFound("Actor not found.");

        var credentials = await _context.Set<ActorCredentials>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ActorId == dto.ActorId && c.IsActive);

        if (credentials == null)
        {
            // No credentials yet — create them using the actor's email as identifier.
            _context.Set<ActorCredentials>().Add(new ActorCredentials
            {
                ActorId   = dto.ActorId,
                Identifier = actor.Email,
                PasswordHash = _hasher.Hash(dto.Password),
                TenantId  = actor.TenantId,
                IsActive  = true
            });
        }
        else
        {
            credentials.PasswordHash = _hasher.Hash(dto.Password);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Flow B: Generate a one-time reset token ───────────────────────────────

    [HttpPost("generate-reset-token")]
    [Authorize]
    [RequirePermission("SuperAdmin.ManageCredentials")]
    public async Task<ActionResult<object>> GenerateResetToken(GenerateResetTokenDto dto)
    {
        var actor = await _context.Set<Actor>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == dto.ActorId);

        if (actor == null) return NotFound("Actor not found.");

        // Expire any existing tokens for this actor before issuing a new one.
        var existing = await _context.Set<PasswordResetToken>()
            .IgnoreQueryFilters()
            .Where(t => t.ActorId == dto.ActorId && !t.IsUsed)
            .ToListAsync();

        foreach (var old in existing)
            old.IsUsed = true;

        var token = new PasswordResetToken
        {
            ActorId   = dto.ActorId,
            TenantId  = actor.TenantId,
            Token     = GenerateToken(),
            ExpiresAt = DateTime.UtcNow.Add(TokenTtl),
            IsUsed    = false
        };

        _context.Set<PasswordResetToken>().Add(token);
        await _context.SaveChangesAsync();

        // Return the token in plain text so the admin can share it out-of-band.
        return Ok(new
        {
            token     = token.Token,
            expiresAt = token.ExpiresAt,
            loginUrl  = $"/reset-password?token={Uri.EscapeDataString(token.Token)}"
        });
    }

    // ── Flow B: Consume token and set new password ────────────────────────────

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var resetToken = await _context.Set<PasswordResetToken>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Token == dto.Token && !t.IsUsed);

        if (resetToken == null || resetToken.ExpiresAt < DateTime.UtcNow)
            return BadRequest("Invalid or expired reset token.");

        var actor = await _context.Set<Actor>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == resetToken.ActorId);

        if (actor == null) return BadRequest("Invalid or expired reset token.");

        var credentials = await _context.Set<ActorCredentials>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.ActorId == resetToken.ActorId && c.IsActive);

        if (credentials == null)
        {
            _context.Set<ActorCredentials>().Add(new ActorCredentials
            {
                ActorId      = resetToken.ActorId,
                Identifier   = actor.Email,
                PasswordHash = _hasher.Hash(dto.Password),
                TenantId     = actor.TenantId,
                IsActive     = true
            });
        }
        else
        {
            credentials.PasswordHash = _hasher.Hash(dto.Password);
        }

        resetToken.IsUsed   = true;
        resetToken.UsedAt   = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
}
