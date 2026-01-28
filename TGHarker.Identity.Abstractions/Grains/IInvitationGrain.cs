using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGHarker.Identity.Abstractions.Grains;

/// <summary>
/// Grain representing a tenant invitation.
/// Key: {tenantId}/invitation-{invitationId}
/// </summary>
public interface IInvitationGrain : IGrainWithStringKey
{
    Task<InvitationState?> GetStateAsync();
    Task<CreateInvitationResult> CreateAsync(CreateInvitationRequest request);
    Task<AcceptInvitationResult> AcceptAsync(string userId);
    Task<bool> RevokeAsync();
    Task<bool> IsValidAsync();
    Task<bool> ExistsAsync();
}
