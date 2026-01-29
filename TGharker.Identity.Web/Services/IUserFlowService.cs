using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public interface IUserFlowService
{
    Task<UserFlowSettings?> GetUserFlowForClientAsync(string tenantId, string clientId);
    Task<UserFlowSettings?> ResolveUserFlowFromReturnUrlAsync(string tenantId, string? returnUrl);
}
