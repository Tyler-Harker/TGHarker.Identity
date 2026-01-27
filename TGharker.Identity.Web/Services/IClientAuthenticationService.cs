using TGHarker.Identity.Abstractions.Models;

namespace TGharker.Identity.Web.Services;

public interface IClientAuthenticationService
{
    Task<ClientAuthenticationResult> AuthenticateAsync(HttpContext context, string tenantId);
}

public sealed class ClientAuthenticationResult
{
    public bool IsSuccess { get; init; }
    public ClientState? Client { get; init; }
    public string? Error { get; init; }
    public string? ErrorDescription { get; init; }

    public static ClientAuthenticationResult Success(ClientState client) =>
        new() { IsSuccess = true, Client = client };

    public static ClientAuthenticationResult Failure(string error, string? description = null) =>
        new() { IsSuccess = false, Error = error, ErrorDescription = description };
}
