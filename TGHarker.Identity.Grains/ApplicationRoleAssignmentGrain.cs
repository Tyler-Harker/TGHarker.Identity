using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;

namespace TGHarker.Identity.Grains;

/// <summary>
/// Grain representing a single application role assignment.
/// Key: {tenantId}/client-{clientId}/assignment-{assignmentId}
/// </summary>
public sealed class ApplicationRoleAssignmentGrain(
    [PersistentState("assignment", "identity")] IPersistentState<ApplicationRoleAssignmentState> state,
    ILogger<ApplicationRoleAssignmentGrain> logger) : Grain, IApplicationRoleAssignmentGrain
{
    public Task<ApplicationRoleAssignmentState?> GetStateAsync()
    {
        if (!state.RecordExists)
            return Task.FromResult<ApplicationRoleAssignmentState?>(null);

        return Task.FromResult<ApplicationRoleAssignmentState?>(state.State);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(state.RecordExists);

    public async Task<bool> CreateAsync(ApplicationRoleAssignmentState assignmentState)
    {
        if (state.RecordExists)
        {
            logger.LogWarning("Assignment already exists: {GrainKey}", this.GetPrimaryKeyString());
            return false;
        }

        state.State = assignmentState;
        state.State.Id = this.GetPrimaryKeyString();
        await state.WriteStateAsync();

        logger.LogInformation("Created role assignment: User={UserId}, Role={RoleId}, Client={ClientId}, Org={OrgId}",
            assignmentState.UserId, assignmentState.RoleId, assignmentState.ClientId, assignmentState.OrganizationId);

        return true;
    }

    public async Task<bool> DeactivateAsync()
    {
        if (!state.RecordExists)
            return false;

        state.State.IsActive = false;
        await state.WriteStateAsync();

        logger.LogInformation("Deactivated role assignment: {GrainKey}", this.GetPrimaryKeyString());
        return true;
    }

    public async Task<bool> ReactivateAsync()
    {
        if (!state.RecordExists)
            return false;

        state.State.IsActive = true;
        await state.WriteStateAsync();

        logger.LogInformation("Reactivated role assignment: {GrainKey}", this.GetPrimaryKeyString());
        return true;
    }

    public async Task<bool> DeleteAsync()
    {
        if (!state.RecordExists)
            return false;

        await state.ClearStateAsync();

        logger.LogInformation("Deleted role assignment: {GrainKey}", this.GetPrimaryKeyString());
        return true;
    }
}
