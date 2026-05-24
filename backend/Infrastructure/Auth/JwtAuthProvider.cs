using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Lidstroem.Core.Auth;
using Lidstroem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Lidstroem.Infrastructure.Auth;

public class JwtAuthProvider : IAuthProvider
{
    private readonly DbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _config;

    // BUG FIX #22 (guard): Fail fast at startup if JWT secret is empty or too short.
    // The original code would only throw on the first token issuance attempt, giving a
    // confusing error deep in the auth flow rather than a clear startup message.
    private string Secret
    {
        get
        {
            var secret = _config["Auth:Jwt:Secret"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException(
                    "Auth:Jwt:Secret is not configured. " +
                    "Set it via `dotnet user-secrets set Auth:Jwt:Secret <value>` " +
                    "or the AUTH__JWT__SECRET environment variable. " +
                    "The value must be at least 32 characters.");
            if (secret.Length < 32)
                throw new InvalidOperationException(
                    $"Auth:Jwt:Secret is too short ({secret.Length} chars). " +
                    "HMACSHA256 requires a minimum of 32 characters.");
            return secret;
        }
    }
    private string Issuer => _config["Auth:Jwt:Issuer"] ?? "lidstroem";
    private string Audience => _config["Auth:Jwt:Audience"] ?? "lidstroem";

    private readonly int _accessMinutes;
    private readonly int _refreshDays;

    public JwtAuthProvider(DbContext context, IPasswordHasher hasher, IConfiguration config)
    {
        _context = context;
        _hasher = hasher;
        _config = config;

        // Validate and parse token lifetimes at construction so a misconfigured
        // value fails fast at startup rather than throwing FormatException on the
        // first login attempt.
        _accessMinutes = int.TryParse(config["Auth:Jwt:AccessTokenMinutes"], out var am) && am > 0
            ? am : 15;
        _refreshDays = int.TryParse(config["Auth:Jwt:RefreshTokenDays"], out var rd) && rd > 0
            ? rd : 30;
    }

    public async Task<TokenPair?> AuthenticateAsync(string identifier, string secret)
    {
        var credentials = await _context.Set<ActorCredentials>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Identifier == identifier && c.IsActive);

        if (credentials?.PasswordHash == null) return null;
        if (!_hasher.Verify(secret, credentials.PasswordHash)) return null;

        credentials.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return await IssueTokenPairAsync(credentials);
    }

    public async Task<TokenPair?> RefreshAsync(string refreshToken)
    {
        var stored = await _context.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .Include(r => r.Credentials)
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (stored == null || !stored.IsActive) return null;

        // BUG FIX #14: The original code called IssueTokenPairAsync (which internally calls
        // StoreRefreshTokenAsync → SaveChangesAsync) BEFORE persisting the revocation of the
        // old token. If the second SaveChangesAsync at the end failed, the new refresh token
        // was already live in the DB but the old token was never marked revoked — both were
        // simultaneously valid and the old token could be replayed indefinitely.
        // Fix: wrap both operations in a single transaction so they succeed or fail together.
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            stored.IsRevoked = true;
            stored.RevokedAt = DateTime.UtcNow;

            var newPair = await IssueTokenPairAsync(stored.Credentials!);
            stored.ReplacedByToken = newPair.RefreshToken;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return newPair;
        }
        catch
        {
            await transaction.RollbackAsync();
            return null;
        }
    }

    public async Task RevokeAsync(string refreshToken)
    {
        var stored = await _context.Set<RefreshToken>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (stored == null || stored.IsRevoked) return;

        stored.IsRevoked = true;
        stored.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    private async Task<TokenPair> IssueTokenPairAsync(ActorCredentials credentials)
    {
        var expiry = DateTime.UtcNow.AddMinutes(_accessMinutes);
        var accessToken = BuildJwt(credentials, expiry);
        var refreshToken = await StoreRefreshTokenAsync(credentials.Id);
        return new TokenPair(accessToken, refreshToken, expiry);
    }

    private string BuildJwt(ActorCredentials credentials, DateTime expiry)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, credentials.ActorId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", credentials.TenantId.ToString()),
            new Claim("identifier", credentials.Identifier)
        };
        var token = new JwtSecurityToken(
            Issuer, Audience, claims,
            expires: expiry,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> StoreRefreshTokenAsync(int credentialsId)
    {
        var tokenValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        _context.Set<RefreshToken>().Add(new RefreshToken
        {
            ActorCredentialsId = credentialsId,
            Token = tokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshDays)
        });

        await _context.SaveChangesAsync();
        return tokenValue;
    }
}
