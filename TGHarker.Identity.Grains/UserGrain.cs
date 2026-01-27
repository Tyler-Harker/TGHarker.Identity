using Orleans.Runtime;
using TGHarker.Identity.Abstractions.Grains;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.Abstractions.Requests;
using System.Security.Cryptography;

namespace TGHarker.Identity.Grains;

public sealed class UserGrain : Grain, IUserGrain
{
    private readonly IPersistentState<UserState> _state;
    private readonly IGrainFactory _grainFactory;
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public UserGrain(
        [PersistentState("user", "Default")] IPersistentState<UserState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public Task<UserState?> GetStateAsync()
    {
        if (string.IsNullOrEmpty(_state.State.Id))
            return Task.FromResult<UserState?>(null);

        return Task.FromResult<UserState?>(_state.State);
    }

    public async Task<CreateUserResult> CreateAsync(CreateUserRequest request)
    {
        if (!string.IsNullOrEmpty(_state.State.Id))
        {
            return new CreateUserResult
            {
                Success = false,
                Error = "User already exists"
            };
        }

        // Extract user ID from grain key (format: "user-{userId}")
        var grainKey = this.GetPrimaryKeyString();
        var userId = grainKey.StartsWith("user-") ? grainKey[5..] : grainKey;

        // Register email in registry
        var registry = _grainFactory.GetGrain<IUserRegistryGrain>("user-registry");
        var registered = await registry.RegisterUserAsync(request.Email.ToLowerInvariant(), userId);

        if (!registered)
        {
            return new CreateUserResult
            {
                Success = false,
                Error = "Email already in use"
            };
        }

        _state.State = new UserState
        {
            Id = userId,
            Email = request.Email.ToLowerInvariant(),
            EmailVerified = false,
            PasswordHash = request.Password, // Should already be hashed by caller
            GivenName = request.GivenName,
            FamilyName = request.FamilyName,
            PhoneNumber = request.PhoneNumber,
            IsActive = true,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            TenantMemberships = []
        };

        // Generate email verification token
        _state.State.EmailVerificationToken = GenerateSecureToken();
        _state.State.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);

        await _state.WriteStateAsync();

        return new CreateUserResult
        {
            Success = true,
            UserId = userId
        };
    }

    public Task<bool> ValidatePasswordAsync(string password)
    {
        // The password parameter should be the hash to compare
        // Actual password verification should happen in the service layer
        return Task.FromResult(_state.State.PasswordHash == password);
    }

    public async Task<bool> ChangePasswordAsync(string currentPasswordHash, string newPasswordHash)
    {
        if (_state.State.PasswordHash != currentPasswordHash)
            return false;

        _state.State.PasswordHash = newPasswordHash;
        await _state.WriteStateAsync();
        return true;
    }

    public async Task SetPasswordHashAsync(string passwordHash)
    {
        _state.State.PasswordHash = passwordHash;
        await _state.WriteStateAsync();
    }

    public async Task<string?> GenerateEmailVerificationTokenAsync()
    {
        if (_state.State.EmailVerified)
            return null;

        _state.State.EmailVerificationToken = GenerateSecureToken();
        _state.State.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        await _state.WriteStateAsync();

        return _state.State.EmailVerificationToken;
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrEmpty(_state.State.EmailVerificationToken))
            return false;

        if (_state.State.EmailVerificationTokenExpiry < DateTime.UtcNow)
            return false;

        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(_state.State.EmailVerificationToken),
            System.Text.Encoding.UTF8.GetBytes(token)))
            return false;

        _state.State.EmailVerified = true;
        _state.State.EmailVerificationToken = null;
        _state.State.EmailVerificationTokenExpiry = null;
        await _state.WriteStateAsync();

        return true;
    }

    public async Task<string?> GeneratePasswordResetTokenAsync()
    {
        _state.State.PasswordResetToken = GenerateSecureToken();
        _state.State.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        await _state.WriteStateAsync();

        return _state.State.PasswordResetToken;
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPasswordHash)
    {
        if (string.IsNullOrEmpty(_state.State.PasswordResetToken))
            return false;

        if (_state.State.PasswordResetTokenExpiry < DateTime.UtcNow)
            return false;

        if (!CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(_state.State.PasswordResetToken),
            System.Text.Encoding.UTF8.GetBytes(token)))
            return false;

        _state.State.PasswordHash = newPasswordHash;
        _state.State.PasswordResetToken = null;
        _state.State.PasswordResetTokenExpiry = null;
        await _state.WriteStateAsync();

        return true;
    }

    public async Task UpdateProfileAsync(UpdateProfileRequest request)
    {
        if (request.GivenName != null)
            _state.State.GivenName = request.GivenName;

        if (request.FamilyName != null)
            _state.State.FamilyName = request.FamilyName;

        if (request.PhoneNumber != null)
            _state.State.PhoneNumber = request.PhoneNumber;

        if (request.Picture != null)
            _state.State.Picture = request.Picture;

        await _state.WriteStateAsync();
    }

    public async Task RecordLoginAttemptAsync(bool success, string? ipAddress)
    {
        if (success)
        {
            _state.State.FailedLoginAttempts = 0;
            _state.State.LastLoginAt = DateTime.UtcNow;
            _state.State.IsLocked = false;
            _state.State.LockoutEnd = null;
        }
        else
        {
            _state.State.FailedLoginAttempts++;

            if (_state.State.FailedLoginAttempts >= MaxFailedAttempts)
            {
                _state.State.IsLocked = true;
                _state.State.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
            }
        }

        await _state.WriteStateAsync();
    }

    public Task<bool> IsLockedOutAsync()
    {
        if (!_state.State.IsLocked)
            return Task.FromResult(false);

        if (_state.State.LockoutEnd.HasValue && _state.State.LockoutEnd.Value < DateTime.UtcNow)
        {
            // Lockout has expired, will be cleared on next successful login
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public async Task UnlockAsync()
    {
        _state.State.IsLocked = false;
        _state.State.LockoutEnd = null;
        _state.State.FailedLoginAttempts = 0;
        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_state.State.Id));
    }

    public Task<IReadOnlyList<string>> GetTenantMembershipsAsync()
    {
        return Task.FromResult<IReadOnlyList<string>>(_state.State.TenantMemberships);
    }

    public async Task AddTenantMembershipAsync(string tenantId)
    {
        if (!_state.State.TenantMemberships.Contains(tenantId))
        {
            _state.State.TenantMemberships.Add(tenantId);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveTenantMembershipAsync(string tenantId)
    {
        if (_state.State.TenantMemberships.Remove(tenantId))
        {
            await _state.WriteStateAsync();
        }
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
