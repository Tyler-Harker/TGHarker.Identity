using Microsoft.AspNetCore.Authorization;
using TGharker.Identity.Web.Services;

namespace TGharker.Identity.Web.Authorization;

/// <summary>
/// Handles authorization for single permission requirements.
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var hasPermission = await _permissionService.HasPermissionAsync(context.User, requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            var userId = context.User.FindFirst("sub")?.Value;
            _logger.LogWarning(
                "Permission denied: User {UserId} does not have permission {Permission}",
                userId, requirement.Permission);
        }
    }
}

/// <summary>
/// Handles authorization for any-of-multiple-permissions requirements.
/// </summary>
public class AnyPermissionAuthorizationHandler : AuthorizationHandler<AnyPermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ILogger<AnyPermissionAuthorizationHandler> _logger;

    public AnyPermissionAuthorizationHandler(
        IPermissionService permissionService,
        ILogger<AnyPermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var hasAnyPermission = await _permissionService.HasAnyPermissionAsync(context.User, requirement.Permissions);

        if (hasAnyPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            var userId = context.User.FindFirst("sub")?.Value;
            _logger.LogWarning(
                "Permission denied: User {UserId} does not have any of permissions [{Permissions}]",
                userId, string.Join(", ", requirement.Permissions));
        }
    }
}
