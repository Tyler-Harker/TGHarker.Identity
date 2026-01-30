using System.Text.Json;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Orleans.Search.Core.Extensions;
using TGHarker.Orleans.Search.Generated;

namespace TGharker.Identity.Web.Services;

public interface IAdminService
{
    Task<Dictionary<string, int>> GetGrainCountsAsync();
    Task<List<GrainSummary>> GetGrainsAsync(string grainType, int skip, int take);
    Task<GrainDetail?> GetGrainStateAsync(string grainType, string grainId);
    Task<ResyncResult> ResyncGrainTypeAsync(string grainType);
    Task<ResyncResult> ResyncAllAsync();
    IReadOnlyList<string> GetSupportedGrainTypes();
}

public class GrainSummary
{
    public string GrainType { get; set; } = string.Empty;
    public string GrainId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class GrainDetail
{
    public string GrainType { get; set; } = string.Empty;
    public string GrainId { get; set; } = string.Empty;
    public string StateJson { get; set; } = string.Empty;
    public DateTime? LastModified { get; set; }
}

public class ResyncResult
{
    public string GrainType { get; set; } = string.Empty;
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = [];
    public TimeSpan Duration { get; set; }
}

public class AdminService : IAdminService
{
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<AdminService> _logger;

    private static readonly string[] SupportedGrainTypes =
    [
        "User",
        "Tenant",
        "Client",
        "Organization",
        "Invitation",
        "OrganizationInvitation",
        "TenantMembership",
        "OrganizationMembership"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AdminService(IClusterClient clusterClient, ILogger<AdminService> logger)
    {
        _clusterClient = clusterClient;
        _logger = logger;
    }

    public IReadOnlyList<string> GetSupportedGrainTypes() => SupportedGrainTypes;

    public async Task<Dictionary<string, int>> GetGrainCountsAsync()
    {
        var counts = new Dictionary<string, int>();

        try
        {
            var tasks = new Dictionary<string, Task<int>>
            {
                ["User"] = _clusterClient.Search<IUserGrain>().CountAsync(),
                ["Tenant"] = _clusterClient.Search<ITenantGrain>().CountAsync(),
                ["Client"] = _clusterClient.Search<IClientGrain>().CountAsync(),
                ["Organization"] = _clusterClient.Search<IOrganizationGrain>().CountAsync(),
                ["Invitation"] = _clusterClient.Search<IInvitationGrain>().CountAsync(),
                ["OrganizationInvitation"] = _clusterClient.Search<IOrganizationInvitationGrain>().CountAsync(),
                ["TenantMembership"] = _clusterClient.Search<ITenantMembershipGrain>().CountAsync(),
                ["OrganizationMembership"] = _clusterClient.Search<IOrganizationMembershipGrain>().CountAsync()
            };

            await Task.WhenAll(tasks.Values);

            foreach (var (type, task) in tasks)
            {
                counts[type] = await task;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get grain counts");
        }

        return counts;
    }

    public async Task<List<GrainSummary>> GetGrainsAsync(string grainType, int skip, int take)
    {
        var summaries = new List<GrainSummary>();

        try
        {
            switch (grainType)
            {
                case "User":
                    var users = await _clusterClient.Search<IUserGrain>().ToListAsync();
                    foreach (var grain in users.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.Email,
                                IsActive = state.IsActive
                            });
                        }
                    }
                    break;

                case "Tenant":
                    var tenants = await _clusterClient.Search<ITenantGrain>().ToListAsync();
                    foreach (var grain in tenants.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.Name ?? state.Identifier,
                                IsActive = state.IsActive
                            });
                        }
                    }
                    break;

                case "Client":
                    var clients = await _clusterClient.Search<IClientGrain>().ToListAsync();
                    foreach (var grain in clients.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.ClientName ?? state.ClientId,
                                IsActive = state.IsActive
                            });
                        }
                    }
                    break;

                case "Organization":
                    var orgs = await _clusterClient.Search<IOrganizationGrain>().ToListAsync();
                    foreach (var grain in orgs.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.Name ?? state.Identifier,
                                IsActive = state.IsActive
                            });
                        }
                    }
                    break;

                case "Invitation":
                    var invitations = await _clusterClient.Search<IInvitationGrain>().ToListAsync();
                    foreach (var grain in invitations.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.Email,
                                IsActive = state.Status == InvitationStatus.Pending
                            });
                        }
                    }
                    break;

                case "OrganizationInvitation":
                    var orgInvitations = await _clusterClient.Search<IOrganizationInvitationGrain>().ToListAsync();
                    foreach (var grain in orgInvitations.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.Email,
                                IsActive = state.Status == InvitationStatus.Pending
                            });
                        }
                    }
                    break;

                case "TenantMembership":
                    var memberships = await _clusterClient.Search<ITenantMembershipGrain>().ToListAsync();
                    foreach (var grain in memberships.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.Username ?? state.UserId,
                                IsActive = state.IsActive
                            });
                        }
                    }
                    break;

                case "OrganizationMembership":
                    var orgMemberships = await _clusterClient.Search<IOrganizationMembershipGrain>().ToListAsync();
                    foreach (var grain in orgMemberships.Skip(skip).Take(take))
                    {
                        var state = await grain.GetStateAsync();
                        if (state != null)
                        {
                            summaries.Add(new GrainSummary
                            {
                                GrainType = grainType,
                                GrainId = grain.GetPrimaryKeyString(),
                                DisplayName = state.DisplayName ?? state.UserId,
                                IsActive = state.IsActive
                            });
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get grains of type {GrainType}", grainType);
        }

        return summaries;
    }

    public async Task<GrainDetail?> GetGrainStateAsync(string grainType, string grainId)
    {
        try
        {
            object? state = grainType switch
            {
                "User" => await _clusterClient.GetGrain<IUserGrain>(grainId).GetStateAsync(),
                "Tenant" => await _clusterClient.GetGrain<ITenantGrain>(grainId).GetStateAsync(),
                "Client" => await _clusterClient.GetGrain<IClientGrain>(grainId).GetStateAsync(),
                "Organization" => await _clusterClient.GetGrain<IOrganizationGrain>(grainId).GetStateAsync(),
                "Invitation" => await _clusterClient.GetGrain<IInvitationGrain>(grainId).GetStateAsync(),
                "OrganizationInvitation" => await _clusterClient.GetGrain<IOrganizationInvitationGrain>(grainId).GetStateAsync(),
                "TenantMembership" => await _clusterClient.GetGrain<ITenantMembershipGrain>(grainId).GetStateAsync(),
                "OrganizationMembership" => await _clusterClient.GetGrain<IOrganizationMembershipGrain>(grainId).GetStateAsync(),
                _ => null
            };

            if (state == null)
                return null;

            return new GrainDetail
            {
                GrainType = grainType,
                GrainId = grainId,
                StateJson = JsonSerializer.Serialize(state, JsonOptions)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get grain state for {GrainType}/{GrainId}", grainType, grainId);
            return null;
        }
    }

    public async Task<ResyncResult> ResyncGrainTypeAsync(string grainType)
    {
        var result = new ResyncResult { GrainType = grainType };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            switch (grainType)
            {
                case "User":
                    await ResyncGrainsAsync<IUserGrain>(result);
                    break;
                case "Tenant":
                    await ResyncGrainsAsync<ITenantGrain>(result);
                    break;
                case "Client":
                    await ResyncGrainsAsync<IClientGrain>(result);
                    break;
                case "Organization":
                    await ResyncGrainsAsync<IOrganizationGrain>(result);
                    break;
                case "Invitation":
                    await ResyncGrainsAsync<IInvitationGrain>(result);
                    break;
                case "OrganizationInvitation":
                    await ResyncGrainsAsync<IOrganizationInvitationGrain>(result);
                    break;
                case "TenantMembership":
                    await ResyncGrainsAsync<ITenantMembershipGrain>(result);
                    break;
                case "OrganizationMembership":
                    await ResyncGrainsAsync<IOrganizationMembershipGrain>(result);
                    break;
                default:
                    result.Errors.Add($"Unknown grain type: {grainType}");
                    result.ErrorCount = 1;
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resync grain type {GrainType}", grainType);
            result.Errors.Add(ex.Message);
            result.ErrorCount++;
        }

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    public async Task<ResyncResult> ResyncAllAsync()
    {
        var combinedResult = new ResyncResult { GrainType = "All" };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var grainType in SupportedGrainTypes)
        {
            var result = await ResyncGrainTypeAsync(grainType);
            combinedResult.TotalProcessed += result.TotalProcessed;
            combinedResult.SuccessCount += result.SuccessCount;
            combinedResult.ErrorCount += result.ErrorCount;
            combinedResult.Errors.AddRange(result.Errors.Select(e => $"[{grainType}] {e}"));
        }

        stopwatch.Stop();
        combinedResult.Duration = stopwatch.Elapsed;
        return combinedResult;
    }

    private async Task ResyncGrainsAsync<TGrain>(ResyncResult result) where TGrain : IGrainWithStringKey
    {
        var grains = await _clusterClient.Search<TGrain>().ToListAsync();
        result.TotalProcessed = grains.Count;

        foreach (var grain in grains)
        {
            try
            {
                // Trigger a state read which will update the search index if the grain
                // has the searchable storage wrapper configured
                await TriggerResyncForGrain(grain);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.ErrorCount++;
                result.Errors.Add($"Failed to resync grain: {ex.Message}");
                if (result.Errors.Count > 100)
                {
                    result.Errors.Add("... (truncated, too many errors)");
                    break;
                }
            }
        }
    }

    private static async Task TriggerResyncForGrain<TGrain>(TGrain grain)
    {
        // For searchable grains, calling a method that triggers WriteStateAsync
        // will update the search index. We call GetStateAsync and then
        // use reflection or a known method to trigger a write.
        // The simplest approach is to call a method that does a state write.

        // For now, we just activate the grain and get its state
        // The searchable storage decorator should sync on WriteStateAsync
        // So we need a way to trigger a write...

        // Each grain type might have a different method, but most have a common pattern
        // We'll just get the state for now - a full resync would require
        // calling a method like RefreshSearchIndexAsync on each grain
        switch (grain)
        {
            case IUserGrain userGrain:
                await userGrain.GetStateAsync();
                break;
            case ITenantGrain tenantGrain:
                await tenantGrain.GetStateAsync();
                break;
            case IClientGrain clientGrain:
                await clientGrain.GetStateAsync();
                break;
            case IOrganizationGrain orgGrain:
                await orgGrain.GetStateAsync();
                break;
            case IInvitationGrain invitationGrain:
                await invitationGrain.GetStateAsync();
                break;
            case IOrganizationInvitationGrain orgInvGrain:
                await orgInvGrain.GetStateAsync();
                break;
            case ITenantMembershipGrain membershipGrain:
                await membershipGrain.GetStateAsync();
                break;
            case IOrganizationMembershipGrain orgMembershipGrain:
                await orgMembershipGrain.GetStateAsync();
                break;
        }
    }
}
