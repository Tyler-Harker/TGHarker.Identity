namespace TGharker.Identity.Web.Services;

/// <summary>
/// Validates redirect URIs for security compliance with OAuth 2.0 / OIDC specifications.
/// </summary>
public static class RedirectUriValidator
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "https",
        "http" // Only for localhost
    };

    private static readonly HashSet<string> DangerousSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript",
        "data",
        "vbscript",
        "file"
    };

    private static readonly HashSet<string> LocalhostHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "127.0.0.1",
        "::1",
        "[::1]"
    };

    /// <summary>
    /// Validates a redirect URI for registration (when a client is created/updated).
    /// </summary>
    public static RedirectUriValidationResult ValidateForRegistration(string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return RedirectUriValidationResult.Invalid("Redirect URI cannot be empty.");
        }

        // Parse the URI
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            return RedirectUriValidationResult.Invalid("Redirect URI must be a valid absolute URI.");
        }

        // Check for dangerous schemes
        if (DangerousSchemes.Contains(uri.Scheme))
        {
            return RedirectUriValidationResult.Invalid($"Scheme '{uri.Scheme}' is not allowed for redirect URIs.");
        }

        // Validate scheme
        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            // Allow custom schemes for native apps (e.g., myapp://callback)
            if (!IsValidCustomScheme(uri.Scheme))
            {
                return RedirectUriValidationResult.Invalid($"Scheme '{uri.Scheme}' is not allowed. Use https, http (localhost only), or a valid custom scheme.");
            }
        }

        // HTTP is only allowed for localhost
        if (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsLocalhost(uri.Host))
            {
                return RedirectUriValidationResult.Invalid("HTTP scheme is only allowed for localhost. Use HTTPS for production.");
            }
        }

        // Fragments are not allowed in redirect URIs (OAuth 2.0 spec)
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return RedirectUriValidationResult.Invalid("Redirect URI cannot contain a fragment (#).");
        }

        // Check for path traversal attempts
        if (ContainsPathTraversal(uri.AbsolutePath))
        {
            return RedirectUriValidationResult.Invalid("Redirect URI contains invalid path sequences.");
        }

        // Validate host
        if (string.IsNullOrEmpty(uri.Host) && !IsCustomScheme(uri.Scheme))
        {
            return RedirectUriValidationResult.Invalid("Redirect URI must have a valid host.");
        }

        // Check for suspicious patterns
        if (ContainsSuspiciousPatterns(redirectUri))
        {
            return RedirectUriValidationResult.Invalid("Redirect URI contains suspicious patterns.");
        }

        return RedirectUriValidationResult.Valid();
    }

    /// <summary>
    /// Validates a redirect URI during authorization request.
    /// </summary>
    public static RedirectUriValidationResult ValidateForAuthorization(string requestedUri, IReadOnlyList<string> registeredUris)
    {
        // First, validate the basic format
        var formatResult = ValidateForRegistration(requestedUri);
        if (!formatResult.IsValid)
        {
            return formatResult;
        }

        // Exact match is required for security
        if (!registeredUris.Contains(requestedUri))
        {
            return RedirectUriValidationResult.Invalid("Redirect URI does not match any registered URI.");
        }

        return RedirectUriValidationResult.Valid();
    }

    private static bool IsLocalhost(string host)
    {
        return LocalhostHosts.Contains(host) ||
               host.StartsWith("127.", StringComparison.Ordinal) ||
               host.Equals("[::1]", StringComparison.Ordinal);
    }

    private static bool IsValidCustomScheme(string scheme)
    {
        // Custom schemes for native apps must:
        // - Start with a letter
        // - Contain only letters, digits, plus, minus, or period
        // - Be at least 2 characters long
        if (string.IsNullOrEmpty(scheme) || scheme.Length < 2)
            return false;

        if (!char.IsLetter(scheme[0]))
            return false;

        foreach (var c in scheme)
        {
            if (!char.IsLetterOrDigit(c) && c != '+' && c != '-' && c != '.')
                return false;
        }

        return true;
    }

    private static bool IsCustomScheme(string scheme)
    {
        return !scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
               !scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsPathTraversal(string path)
    {
        // Check for common path traversal patterns
        var decoded = Uri.UnescapeDataString(path);
        return decoded.Contains("..") ||
               decoded.Contains("//") ||
               path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("%252e", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSuspiciousPatterns(string uri)
    {
        var lower = uri.ToLowerInvariant();

        // Check for URL encoding tricks
        if (lower.Contains("%00") || // Null byte
            lower.Contains("%0d") || // CR
            lower.Contains("%0a") || // LF
            lower.Contains("@"))     // Credential injection
        {
            return true;
        }

        // Check for backslash (sometimes interpreted as forward slash)
        if (uri.Contains('\\'))
        {
            return true;
        }

        return false;
    }
}

public sealed class RedirectUriValidationResult
{
    public bool IsValid { get; private init; }
    public string? Error { get; private init; }

    private RedirectUriValidationResult() { }

    public static RedirectUriValidationResult Valid() => new() { IsValid = true };
    public static RedirectUriValidationResult Invalid(string error) => new() { IsValid = false, Error = error };
}
