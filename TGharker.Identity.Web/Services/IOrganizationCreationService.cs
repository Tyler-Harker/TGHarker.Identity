using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public interface IOrganizationCreationService
{
    Task<OrganizationCreationResult> CreateOrganizationForUserAsync(
        string tenantId,
        string userId,
        string userEmail,
        string? userGivenName,
        string? organizationName,
        string? organizationIdentifier,
        UserFlowSettings userFlow);
}

public sealed class OrganizationCreationResult
{
    public bool Success { get; set; }
    public string? OrganizationId { get; set; }
    public string? Error { get; set; }
}
