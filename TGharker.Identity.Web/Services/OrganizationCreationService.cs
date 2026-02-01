using System.Text.RegularExpressions;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;

namespace TGharker.Identity.Web.Services;

public sealed partial class OrganizationCreationService : IOrganizationCreationService
{
    private readonly IClusterClient _clusterClient;
    private readonly IGrainSearchService _searchService;

    public OrganizationCreationService(
        IClusterClient clusterClient,
        IGrainSearchService searchService)
    {
        _clusterClient = clusterClient;
        _searchService = searchService;
    }

    public async Task<OrganizationCreationResult> CreateOrganizationForUserAsync(
        string tenantId,
        string userId,
        string userEmail,
        string? userGivenName,
        string? organizationName,
        string? organizationIdentifier,
        UserFlowSettings userFlow)
    {
        if (!userFlow.OrganizationsEnabled)
        {
            return new OrganizationCreationResult
            {
                Success = false,
                Error = "Organizations are not enabled for this application"
            };
        }

        // Determine organization name based on mode
        string finalOrgName;
        string finalOrgIdentifier;

        switch (userFlow.OrganizationMode)
        {
            case OrganizationRegistrationMode.None:
                return new OrganizationCreationResult
                {
                    Success = false,
                    Error = "Organization creation is not enabled for this flow"
                };

            case OrganizationRegistrationMode.AutoCreate:
                // Generate name from user info
                finalOrgName = GenerateOrganizationName(userEmail, userGivenName);
                finalOrgIdentifier = GenerateOrganizationIdentifier(finalOrgName);
                break;

            case OrganizationRegistrationMode.Prompt:
                // Require user-provided name
                if (string.IsNullOrWhiteSpace(organizationName))
                {
                    return new OrganizationCreationResult
                    {
                        Success = false,
                        Error = "Organization name is required"
                    };
                }
                finalOrgName = organizationName.Trim();
                finalOrgIdentifier = string.IsNullOrWhiteSpace(organizationIdentifier)
                    ? GenerateOrganizationIdentifier(finalOrgName)
                    : organizationIdentifier.Trim().ToLowerInvariant();
                break;

            case OrganizationRegistrationMode.OptionalPrompt:
                // Only create if user provided a name
                if (string.IsNullOrWhiteSpace(organizationName))
                {
                    return new OrganizationCreationResult
                    {
                        Success = true,
                        OrganizationId = null // No org created, but that's OK
                    };
                }
                finalOrgName = organizationName.Trim();
                finalOrgIdentifier = string.IsNullOrWhiteSpace(organizationIdentifier)
                    ? GenerateOrganizationIdentifier(finalOrgName)
                    : organizationIdentifier.Trim().ToLowerInvariant();
                break;

            default:
                return new OrganizationCreationResult
                {
                    Success = false,
                    Error = "Invalid organization mode"
                };
        }

        // Check if identifier already exists
        var existingOrg = await _searchService.GetOrganizationByIdentifierAsync(tenantId, finalOrgIdentifier);
        if (existingOrg != null)
        {
            // Append a unique suffix
            finalOrgIdentifier = $"{finalOrgIdentifier}-{Guid.NewGuid().ToString()[..8]}";
        }

        // Create the organization
        var orgId = Guid.CreateVersion7().ToString();
        var orgGrain = _clusterClient.GetGrain<IOrganizationGrain>($"{tenantId}/org-{orgId}");

        var createResult = await orgGrain.InitializeAsync(new CreateOrganizationRequest
        {
            TenantId = tenantId,
            Identifier = finalOrgIdentifier,
            Name = finalOrgName,
            CreatorUserId = userId,
            Settings = new OrganizationSettings
            {
                AllowSelfRegistration = false,
                RequireEmailVerification = true
            }
        });

        if (!createResult.Success)
        {
            return new OrganizationCreationResult
            {
                Success = false,
                Error = createResult.Error ?? "Failed to create organization"
            };
        }

        // Create membership for the user as owner
        var membershipGrain = _clusterClient.GetGrain<IOrganizationMembershipGrain>(
            $"{tenantId}/org-{orgId}/member-{userId}");

        var membershipResult = await membershipGrain.CreateAsync(new CreateOrganizationMembershipRequest
        {
            UserId = userId,
            OrganizationId = orgId,
            TenantId = tenantId,
            Roles = [userFlow.DefaultOrganizationRole ?? WellKnownOrganizationRoles.Owner]
        });

        if (!membershipResult.Success)
        {
            return new OrganizationCreationResult
            {
                Success = false,
                Error = membershipResult.Error ?? "Failed to create organization membership"
            };
        }

        // Add user to organization's member list
        await orgGrain.AddMemberAsync(userId);

        // Add organization to user's membership list
        var userGrain = _clusterClient.GetGrain<IUserGrain>($"user-{userId}");
        await userGrain.AddOrganizationMembershipAsync(tenantId, orgId);

        return new OrganizationCreationResult
        {
            Success = true,
            OrganizationId = orgId
        };
    }

    private static string GenerateOrganizationName(string email, string? givenName)
    {
        if (!string.IsNullOrWhiteSpace(givenName))
        {
            return $"{givenName}'s Organization";
        }

        // Extract name from email
        var emailPart = email.Split('@')[0];
        // Capitalize first letter
        if (emailPart.Length > 0)
        {
            emailPart = char.ToUpper(emailPart[0]) + emailPart[1..];
        }
        return $"{emailPart}'s Organization";
    }

    private static string GenerateOrganizationIdentifier(string name)
    {
        // Convert to lowercase, replace spaces and special chars with hyphens
        var identifier = name.ToLowerInvariant();
        identifier = IdentifierRegex().Replace(identifier, "-");
        identifier = MultipleHyphensRegex().Replace(identifier, "-");
        identifier = identifier.Trim('-');

        // Ensure it starts with a letter or number
        if (identifier.Length == 0 || !char.IsLetterOrDigit(identifier[0]))
        {
            identifier = "org-" + identifier;
        }

        // Limit length
        if (identifier.Length > 50)
        {
            identifier = identifier[..50].TrimEnd('-');
        }

        return identifier;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();
}
