using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using TGHarker.Identity.Abstractions.Grains;

namespace TGharker.Identity.Web.Services;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IClusterClient _clusterClient;

    public JwtTokenGenerator(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<string> GenerateAccessTokenAsync(TokenGenerationContext context)
    {
        var signingCredentials = await GetSigningCredentialsAsync(context.TenantId);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, context.Subject),
            new(JwtRegisteredClaimNames.Iss, context.Issuer),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("client_id", context.ClientId),
            new("tenant_id", context.TenantId),
            new("scope", string.Join(" ", context.Scopes))
        };

        // Add additional resource claims
        foreach (var claim in context.AdditionalClaims)
        {
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddMinutes(context.AccessTokenLifetimeMinutes),
            Issuer = context.Issuer,
            Audience = context.Audience ?? context.ClientId,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task<string> GenerateIdTokenAsync(TokenGenerationContext context)
    {
        var signingCredentials = await GetSigningCredentialsAsync(context.TenantId);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, context.Subject),
            new(JwtRegisteredClaimNames.Iss, context.Issuer),
            new(JwtRegisteredClaimNames.Aud, context.ClientId),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(JwtRegisteredClaimNames.Exp, new DateTimeOffset(now.AddMinutes(context.IdTokenLifetimeMinutes)).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("tenant_id", context.TenantId)
        };

        if (context.AuthTime.HasValue)
        {
            claims.Add(new Claim("auth_time", new DateTimeOffset(context.AuthTime.Value).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64));
        }

        if (!string.IsNullOrEmpty(context.Nonce))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Nonce, context.Nonce));
        }

        // Add identity claims based on scopes
        foreach (var claim in context.IdentityClaims)
        {
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddMinutes(context.IdTokenLifetimeMinutes),
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private async Task<SigningCredentials> GetSigningCredentialsAsync(string tenantId)
    {
        var signingKeyGrain = _clusterClient.GetGrain<ISigningKeyGrain>($"{tenantId}/signing-keys");
        var activeKey = await signingKeyGrain.GetActiveKeyAsync();

        if (activeKey == null)
        {
            // Generate a new key if none exists
            await signingKeyGrain.GenerateNewKeyAsync();
            activeKey = await signingKeyGrain.GetActiveKeyAsync();
        }

        if (activeKey == null)
        {
            throw new InvalidOperationException("Failed to get or create signing key");
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(activeKey.PrivateKeyPem);

        var rsaKey = new RsaSecurityKey(rsa) { KeyId = activeKey.KeyId };
        return new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
    }
}
