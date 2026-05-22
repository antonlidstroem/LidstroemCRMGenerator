namespace Lidstroem.Core.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
    Guid? OwnerId { get; }
    bool IsSystemContext { get; }
}

public interface ICurrentUserContext
{
    string? UserId { get; }
    string? DisplayName { get; }
}

public interface IPasswordHasher
{
    string Hash(string plaintext);
    bool Verify(string plaintext, string hash);
}

public interface IAuthProvider
{
    Task<TokenPair?> AuthenticateAsync(string identifier, string secret);
    Task<TokenPair?> RefreshAsync(string refreshToken);
    Task RevokeAsync(string refreshToken);
}

public record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int actorId, Guid tenantId, string permission);
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(int actorId, Guid tenantId);
    Task AssignRoleAsync(int actorId, Guid tenantId, string roleName);
    Task RevokeRoleAsync(int actorId, Guid tenantId, string roleName);
    void InvalidateCache(int actorId, Guid tenantId);
}

public interface IPermissionProvider
{
    IReadOnlyCollection<PermissionDefinition> GetPermissions();
}

public record PermissionDefinition(
    string Name,
    string DisplayName,
    string Description,
    string Category);
