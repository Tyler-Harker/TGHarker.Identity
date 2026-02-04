using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Grains;

public sealed class UserApplicationRolesGrain : Grain, IUserApplicationRolesGrain
{
    private readonly IPersistentState<UserApplicationRolesState> _state;
    private readonly IGrainFactory _grainFactory;

    // Cached from grain key
    private string? _tenantId;
    private string? _clientId;
    private string? _userId;

    public UserApplicationRolesGrain(
        [PersistentState("user_app_roles", "Default")] IPersistentState<UserApplicationRolesState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        ParseGrainKey();
        return base.OnActivateAsync(cancellationToken);
    }

    private void ParseGrainKey()
    {
        // Key format: {tenantId}/client-{clientId}/user-{userId}
        var key = this.GetPrimaryKeyString();
        var parts = key.Split('/');

        if (parts.Length >= 3)
        {
            _tenantId = parts[0];

            if (parts[1].StartsWith("client-"))
                _clientId = parts[1]["client-".Length..];

            if (parts[2].StartsWith("user-"))
                _userId = parts[2]["user-".Length..];
        }
    }

    public Task<UserApplicationRolesState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.UserId))
            return Task.FromResult<UserApplicationRolesState?>(null);

        return Task.FromResult<UserApplicationRolesState?>(_state.State);
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.UserId));
    }

    public async Task AssignRoleAsync(string roleId, ApplicationRoleScope scope, string? organizationId = null)
    {
        if (scope == ApplicationRoleScope.Organization && string.IsNullOrEmpty(organizationId))
        {
            throw new ArgumentException("OrganizationId is required for organization-scoped roles");
        }

        // Initialize state if needed
        if (string.IsNullOrEmpty(_state.State.UserId))
        {
            _state.State.UserId = _userId ?? throw new InvalidOperationException("Could not parse userId from grain key");
            _state.State.TenantId = _tenantId ?? throw new InvalidOperationException("Could not parse tenantId from grain key");
            _state.State.ClientId = _clientId ?? throw new InvalidOperationException("Could not parse clientId from grain key");
        }

        // Check if assignment already exists
        var existing = _state.State.RoleAssignments.FirstOrDefault(a =>
            a.RoleId == roleId &&
            a.Scope == scope &&
            a.OrganizationId == organizationId);

        if (existing != null)
        {
            return; // Already assigned
        }

        var assignedAt = DateTime.UtcNow;

        _state.State.RoleAssignments.Add(new ApplicationRoleAssignment
        {
            RoleId = roleId,
            Scope = scope,
            OrganizationId = organizationId,
            AssignedAt = assignedAt
        });

        await _state.WriteStateAsync();

        // Also create searchable assignment grain
        var assignmentId = GenerateAssignmentId(roleId, organizationId);
        var assignmentGrain = _grainFactory.GetGrain<IApplicationRoleAssignmentGrain>(
            $"{_tenantId}/client-{_clientId}/assignment-{assignmentId}");

        await assignmentGrain.CreateAsync(new ApplicationRoleAssignmentState
        {
            UserId = _userId!,
            TenantId = _tenantId!,
            ClientId = _clientId!,
            ApplicationId = _clientId!, // Same as ClientId for consistency
            RoleId = roleId,
            Scope = scope,
            OrganizationId = organizationId,
            AssignedAt = assignedAt,
            IsActive = true
        });
    }

    private string GenerateAssignmentId(string roleId, string? organizationId)
    {
        // Create a deterministic ID based on user, role and org
        if (string.IsNullOrEmpty(organizationId))
        {
            return $"user-{_userId}-role-{roleId}";
        }
        return $"user-{_userId}-role-{roleId}-org-{organizationId}";
    }

    public async Task RemoveRoleAsync(string roleId, string? organizationId = null)
    {
        int removed;

        if (organizationId == null)
        {
            // Remove tenant-scoped assignment with this role
            removed = _state.State.RoleAssignments.RemoveAll(a =>
                a.RoleId == roleId && a.Scope == ApplicationRoleScope.Tenant);
        }
        else
        {
            // Remove specific organization-scoped assignment
            removed = _state.State.RoleAssignments.RemoveAll(a =>
                a.RoleId == roleId &&
                a.Scope == ApplicationRoleScope.Organization &&
                a.OrganizationId == organizationId);
        }

        if (removed > 0)
        {
            await _state.WriteStateAsync();

            // Also delete the searchable assignment grain
            var assignmentId = GenerateAssignmentId(roleId, organizationId);
            var assignmentGrain = _grainFactory.GetGrain<IApplicationRoleAssignmentGrain>(
                $"{_tenantId}/client-{_clientId}/assignment-{assignmentId}");

            await assignmentGrain.DeleteAsync();
        }
    }

    public Task<IReadOnlyList<string>> GetEffectiveRolesAsync(string? organizationId = null)
    {
        var roles = new List<string>();

        foreach (var assignment in _state.State.RoleAssignments)
        {
            if (assignment.Scope == ApplicationRoleScope.Tenant)
            {
                // Tenant-scoped roles always apply
                roles.Add(assignment.RoleId);
            }
            else if (assignment.Scope == ApplicationRoleScope.Organization &&
                     organizationId != null &&
                     assignment.OrganizationId == organizationId)
            {
                // Organization-scoped roles only apply if context matches
                roles.Add(assignment.RoleId);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(roles.Distinct().ToList());
    }

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(string? organizationId = null)
    {
        var permissions = new HashSet<string>();

        if (string.IsNullOrEmpty(_tenantId) || string.IsNullOrEmpty(_clientId))
        {
            return permissions;
        }

        // Get the client grain to resolve role definitions
        var clientGrain = _grainFactory.GetGrain<IClientGrain>($"{_tenantId}/{_clientId}");
        var appRoles = await clientGrain.GetApplicationRolesAsync();
        var roleMap = appRoles.ToDictionary(r => r.Id);

        // Get effective roles for this context
        var effectiveRoles = await GetEffectiveRolesAsync(organizationId);

        // Resolve permissions from roles
        foreach (var roleId in effectiveRoles)
        {
            if (roleMap.TryGetValue(roleId, out var role))
            {
                foreach (var permission in role.Permissions)
                {
                    permissions.Add(permission);
                }
            }
        }

        return permissions;
    }

    public Task<IReadOnlyList<ApplicationRoleAssignment>> GetRoleAssignmentsAsync()
    {
        return Task.FromResult<IReadOnlyList<ApplicationRoleAssignment>>(_state.State.RoleAssignments);
    }

    public async Task ClearAllRolesAsync()
    {
        if (_state.State.RoleAssignments.Count > 0)
        {
            // Delete all assignment grains
            var deleteTasks = _state.State.RoleAssignments.Select(a =>
            {
                var assignmentId = GenerateAssignmentId(a.RoleId, a.OrganizationId);
                var assignmentGrain = _grainFactory.GetGrain<IApplicationRoleAssignmentGrain>(
                    $"{_tenantId}/client-{_clientId}/assignment-{assignmentId}");
                return assignmentGrain.DeleteAsync();
            });

            await Task.WhenAll(deleteTasks);

            _state.State.RoleAssignments.Clear();
            await _state.WriteStateAsync();
        }
    }
}
