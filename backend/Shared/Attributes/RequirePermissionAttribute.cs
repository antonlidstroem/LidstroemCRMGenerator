using System.IdentityModel.Tokens.Jwt;
using Lidstroem.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Lidstroem.Shared.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var actorIdClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var tenantIdClaim = user.FindFirst("tenant_id")?.Value;

        if (!int.TryParse(actorIdClaim, out var actorId)
         || !Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();

        if (!await permissionService.HasPermissionAsync(actorId, tenantId, _permission))
            context.Result = new ForbidResult();
    }
}
