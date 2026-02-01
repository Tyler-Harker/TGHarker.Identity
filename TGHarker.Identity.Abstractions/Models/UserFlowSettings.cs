namespace TGHarker.Identity.Abstractions.Models;

/// <summary>
/// Configuration settings that control user registration behavior for a client application.
/// </summary>
[GenerateSerializer]
public sealed class UserFlowSettings
{
    /// <summary>
    /// Whether organizations are supported for users registering through this client.
    /// When false, organization-related fields and prompts are hidden.
    /// </summary>
    [Id(0)] public bool OrganizationsEnabled { get; set; } = false;

    /// <summary>
    /// The behavior for organization assignment during registration.
    /// </summary>
    [Id(1)] public OrganizationRegistrationMode OrganizationMode { get; set; } = OrganizationRegistrationMode.None;

    /// <summary>
    /// When OrganizationMode is AutoCreate, this is the default role assigned to the user in the new organization.
    /// </summary>
    [Id(2)] public string? DefaultOrganizationRole { get; set; } = WellKnownOrganizationRoles.Owner;

    /// <summary>
    /// Whether to require organization name/identifier during prompted registration.
    /// Only applicable when OrganizationMode is Prompt.
    /// </summary>
    [Id(3)] public bool RequireOrganizationName { get; set; } = true;

    /// <summary>
    /// Custom label for the organization name field (e.g., "Company Name", "Team Name").
    /// </summary>
    [Id(4)] public string? OrganizationNameLabel { get; set; }

    /// <summary>
    /// Custom placeholder text for the organization name field.
    /// </summary>
    [Id(5)] public string? OrganizationNamePlaceholder { get; set; }

    /// <summary>
    /// Custom help text displayed below the organization name field.
    /// </summary>
    [Id(6)] public string? OrganizationHelpText { get; set; }

    /// <summary>
    /// The default tenant role assigned to users who register through this client.
    /// Defaults to "guest" - guests have no tenant-level privileges and only have
    /// access within their organizations for the applications they use.
    /// </summary>
    [Id(7)] public string DefaultTenantRole { get; set; } = WellKnownRoles.Guest;
}

/// <summary>
/// Defines how organizations are handled during user registration.
/// </summary>
public enum OrganizationRegistrationMode
{
    /// <summary>
    /// Organizations are not created during registration.
    /// User is added to tenant only.
    /// </summary>
    None = 0,

    /// <summary>
    /// Automatically create an organization for the user upon registration.
    /// Organization name is derived from user's name or email.
    /// </summary>
    AutoCreate = 1,

    /// <summary>
    /// Prompt the user to enter organization information during registration.
    /// </summary>
    Prompt = 2,

    /// <summary>
    /// User can optionally create an organization during registration.
    /// Shows organization fields but they are not required.
    /// </summary>
    OptionalPrompt = 3
}
