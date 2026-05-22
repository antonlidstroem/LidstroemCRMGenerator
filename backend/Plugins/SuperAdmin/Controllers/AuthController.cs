using Lidstroem.Core.Auth;
using Lidstroem.Core.Entities;
using Lidstroem.Core.Interfaces;
using Lidstroem.Shared.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Lidstroem.Plugins.SuperAdmin.Controllers;

public class LoginDto
{
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RegisterDto
{
    public string Identifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int ActorId { get; set; }
}

public class TokenResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthProvider _authProvider;
    private readonly IPasswordHasher _hasher;
    private readonly DbContext _context;

    public AuthController(IAuthProvider authProvider, IPasswordHasher hasher, DbContext context)
    {
        _authProvider = authProvider;
        _hasher = hasher;
        _context = context;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponseDto>> Login(LoginDto dto)
    {
        var pair = await _authProvider.AuthenticateAsync(dto.Identifier, dto.Password);
        // FIX #10: Generic error — never confirm whether identifier exists
        if (pair == null) return Unauthorized("Invalid credentials.");

        return Ok(new TokenResponseDto
        {
            AccessToken = pair.AccessToken,
            RefreshToken = pair.RefreshToken,
            ExpiresAt = pair.ExpiresAt
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponseDto>> Refresh(RefreshDto dto)
    {
        var pair = await _authProvider.RefreshAsync(dto.RefreshToken);
        if (pair == null) return Unauthorized("Invalid or expired token.");

        return Ok(new TokenResponseDto
        {
            AccessToken = pair.AccessToken,
            RefreshToken = pair.RefreshToken,
            ExpiresAt = pair.ExpiresAt
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshDto dto)
    {
        await _authProvider.RevokeAsync(dto.RefreshToken);
        return NoContent();
    }

    // FIX #1: Require explicit SuperAdmin permission — [Authorize] alone allowed any logged-in user.
    // FIX #8: Actor is looked up first so its TenantId is stamped on the new credentials.
    // FIX #10: Conflict response no longer echoes back the identifier.
    [HttpPost("register")]
    [Authorize]
    [RequirePermission("SuperAdmin.ManageCredentials")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        // Resolve the target actor so we can inherit its TenantId
        var actor = await _context.Set<Actor>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == dto.ActorId);

        if (actor == null) return NotFound("Actor not found.");

        var exists = await _context.Set<ActorCredentials>()
            .IgnoreQueryFilters()
            .AnyAsync(c => c.Identifier == dto.Identifier);

        // FIX #10: Do not reveal whether the identifier already exists
        if (exists) return Conflict("Registration failed.");

        _context.Set<ActorCredentials>().Add(new ActorCredentials
        {
            ActorId = dto.ActorId,
            Identifier = dto.Identifier,
            PasswordHash = _hasher.Hash(dto.Password),
            // FIX #8: Explicitly stamp TenantId from the resolved actor
            TenantId = actor.TenantId
        });

        await _context.SaveChangesAsync();
        return Created(string.Empty, null);
    }
}
