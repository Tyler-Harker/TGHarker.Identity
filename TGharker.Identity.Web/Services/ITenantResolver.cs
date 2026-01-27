using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public interface ITenantResolver
{
    Task<TenantState?> ResolveAsync(HttpContext context);
    string? GetTenantIdentifier(HttpContext context);
}
