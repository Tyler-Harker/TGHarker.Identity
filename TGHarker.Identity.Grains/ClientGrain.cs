using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using System.Security.Cryptography;

namespace TGHarker.Identity.Grains;

public sealed class ClientGrain : Grain, IClientGrain
{
    private readonly IPersistentState<ClientState> _state;

    public ClientGrain(
        [PersistentState("client", "Default")] IPersistentState<ClientState> state)
    {
        _state = state;
    }

    public Task<ClientState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult<ClientState?>(null);

        return Task.FromResult<ClientState?>(_state.State);
    }

    public async Task<CreateClientResult> CreateAsync(CreateClientRequest request)
    {
        if (!string.IsNullOrEmpty(_state.State.Id))
        {
            return new CreateClientResult
            {
                Success = false,
                Error = "Client already exists"
            };
        }

        _state.State = new ClientState
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = request.TenantId,
            ClientId = request.ClientId,
            ClientName = request.ClientName,
            Description = request.Description,
            IsConfidential = request.IsConfidential,
            IsActive = true,
            RequireConsent = request.RequireConsent,
            RequirePkce = request.RequirePkce,
            RedirectUris = request.RedirectUris.ToList(),
            AllowedScopes = request.AllowedScopes.ToList(),
            AllowedGrantTypes = request.AllowedGrantTypes.ToList(),
            CorsOrigins = request.CorsOrigins.ToList(),
            PostLogoutRedirectUris = request.PostLogoutRedirectUris.ToList(),
            CreatedAt = DateTime.UtcNow,
            UserFlow = request.UserFlow ?? new UserFlowSettings()
        };

        string? plainTextSecret = null;

        // Generate initial secret for confidential clients
        if (request.IsConfidential)
        {
            plainTextSecret = GenerateClientSecret();
            var secretHash = HashSecret(plainTextSecret);

            _state.State.Secrets.Add(new ClientSecret
            {
                Id = Guid.NewGuid().ToString(),
                Description = "Initial secret",
                SecretHash = secretHash,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _state.WriteStateAsync();

        // Invalidate CORS cache if origins were configured
        if (request.CorsOrigins.Count > 0)
        {
            await InvalidateCorsCacheAsync();
        }

        return new CreateClientResult
        {
            Success = true,
            ClientSecret = plainTextSecret
        };
    }

    public async Task<string?> UpdateAsync(UpdateClientRequest request)
    {
        var corsOriginsChanged = request.CorsOrigins != null;
        string? newSecret = null;

        if (request.ClientName != null)
            _state.State.ClientName = request.ClientName;

        if (request.Description != null)
            _state.State.Description = request.Description;

        if (request.RequireConsent.HasValue)
            _state.State.RequireConsent = request.RequireConsent.Value;

        if (request.RequirePkce.HasValue)
            _state.State.RequirePkce = request.RequirePkce.Value;

        if (request.RedirectUris != null)
            _state.State.RedirectUris = request.RedirectUris.ToList();

        if (request.AllowedScopes != null)
            _state.State.AllowedScopes = request.AllowedScopes.ToList();

        if (request.AllowedGrantTypes != null)
            _state.State.AllowedGrantTypes = request.AllowedGrantTypes.ToList();

        if (request.CorsOrigins != null)
            _state.State.CorsOrigins = request.CorsOrigins.ToList();

        if (request.AccessTokenLifetimeMinutes.HasValue)
            _state.State.AccessTokenLifetimeMinutes = request.AccessTokenLifetimeMinutes.Value;

        if (request.RefreshTokenLifetimeDays.HasValue)
            _state.State.RefreshTokenLifetimeDays = request.RefreshTokenLifetimeDays.Value;

        if (request.PostLogoutRedirectUris != null)
            _state.State.PostLogoutRedirectUris = request.PostLogoutRedirectUris.ToList();

        if (request.UserFlow != null)
            _state.State.UserFlow = request.UserFlow;

        // Handle application type change
        if (request.IsConfidential.HasValue && request.IsConfidential.Value != _state.State.IsConfidential)
        {
            _state.State.IsConfidential = request.IsConfidential.Value;

            // If changing to confidential and no secrets exist, generate one
            if (request.IsConfidential.Value && _state.State.Secrets.Count == 0)
            {
                var plainTextSecret = GenerateClientSecret();
                var secretHash = HashSecret(plainTextSecret);

                // Clear any existing secrets - only one active secret allowed
                _state.State.Secrets.Clear();

                _state.State.Secrets.Add(new ClientSecret
                {
                    Id = Guid.NewGuid().ToString(),
                    Description = "Generated on type change",
                    SecretHash = secretHash,
                    CreatedAt = DateTime.UtcNow
                });

                newSecret = plainTextSecret;
            }
        }

        await _state.WriteStateAsync();

        // Invalidate CORS cache if origins changed
        if (corsOriginsChanged)
        {
            await InvalidateCorsCacheAsync();
        }

        return newSecret;
    }

    public Task<bool> ValidateSecretAsync(string secret)
    {
        var secretHash = HashSecret(secret);

        foreach (var storedSecret in _state.State.Secrets)
        {
            // Check expiration
            if (storedSecret.ExpiresAt.HasValue && storedSecret.ExpiresAt.Value < DateTime.UtcNow)
                continue;

            if (CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(storedSecret.SecretHash),
                System.Text.Encoding.UTF8.GetBytes(secretHash)))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public async Task<AddSecretResult> AddSecretAsync(string description, DateTime? expiresAt)
    {
        var plainTextSecret = GenerateClientSecret();
        var secretHash = HashSecret(plainTextSecret);
        var secretId = Guid.NewGuid().ToString();

        // Clear existing secrets - only one active secret allowed at a time
        _state.State.Secrets.Clear();

        _state.State.Secrets.Add(new ClientSecret
        {
            Id = secretId,
            Description = description,
            SecretHash = secretHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        });

        await _state.WriteStateAsync();

        return new AddSecretResult
        {
            Success = true,
            SecretId = secretId,
            PlainTextSecret = plainTextSecret
        };
    }

    public async Task<bool> RevokeSecretAsync(string secretId)
    {
        var removed = _state.State.Secrets.RemoveAll(s => s.Id == secretId) > 0;

        if (removed)
        {
            await _state.WriteStateAsync();
        }

        return removed;
    }

    public Task<bool> ValidateRedirectUriAsync(string redirectUri)
    {
        // Exact match required for security
        return Task.FromResult(_state.State.RedirectUris.Contains(redirectUri));
    }

    public Task<bool> ValidateScopeAsync(string scope)
    {
        return Task.FromResult(_state.State.AllowedScopes.Contains(scope));
    }

    public Task<bool> ValidateGrantTypeAsync(string grantType)
    {
        return Task.FromResult(_state.State.AllowedGrantTypes.Contains(grantType));
    }

    public Task<bool> ValidatePostLogoutRedirectUriAsync(string uri)
    {
        return Task.FromResult(_state.State.PostLogoutRedirectUris.Contains(uri));
    }

    public Task<bool> IsActiveAsync()
    {
        return Task.FromResult(_state.State.IsActive);
    }

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        await _state.WriteStateAsync();

        // Invalidate CORS cache since client is now active
        if (_state.State.CorsOrigins.Count > 0)
        {
            await InvalidateCorsCacheAsync();
        }
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive = false;
        await _state.WriteStateAsync();

        // Invalidate CORS cache since client is now inactive
        if (_state.State.CorsOrigins.Count > 0)
        {
            await InvalidateCorsCacheAsync();
        }
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.Id));
    }

    public Task<UserFlowSettings> GetUserFlowSettingsAsync()
    {
        return Task.FromResult(_state.State.UserFlow ?? new UserFlowSettings());
    }

    private static string GenerateClientSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashSecret(string secret)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
        return Convert.ToBase64String(hash);
    }

    private async Task InvalidateCorsCacheAsync()
    {
        var corsGrain = GrainFactory.GetGrain<ICorsOriginsGrain>($"{_state.State.TenantId}/cors-origins");
        await corsGrain.InvalidateAsync();
    }
}
