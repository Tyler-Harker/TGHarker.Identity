using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing an organization invitation.
/// Key: {tenantId}/org-{orgId}/invitation-{invitationId}
/// </summary>
public interface IOrganizationInvitationGrain : IGrainWithStringKey
{
    Task<OrganizationInvitationState?> GetStateAsync();
    Task<CreateOrganizationInvitationResult> CreateAsync(CreateOrganizationInvitationRequest request);
    Task<AcceptOrganizationInvitationResult> AcceptAsync(string userId);
    Task<bool> RevokeAsync();
    Task<bool> IsValidAsync();
    Task<bool> ExistsAsync();
}
