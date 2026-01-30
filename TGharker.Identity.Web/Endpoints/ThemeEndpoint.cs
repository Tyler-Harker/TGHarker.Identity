namespace TGharker.Identity.Web.Endpoints;

public static class ThemeEndpoint
{
    public const string ThemeCookieName = "theme";
    public const string DefaultTheme = "dark";

    public static IEndpointRouteBuilder MapThemeEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/theme", SetTheme)
            .AllowAnonymous()
            .WithName("SetTheme")
            .WithTags("Theme")
            .Produces(StatusCodes.Status200OK);

        endpoints.MapGet("/api/theme", GetTheme)
            .AllowAnonymous()
            .WithName("GetTheme")
            .WithTags("Theme")
            .Produces<ThemeResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static IResult SetTheme(HttpContext context, ThemeRequest request)
    {
        var theme = request.Theme?.ToLowerInvariant();

        // Validate theme value
        if (theme != "dark" && theme != "light")
        {
            theme = DefaultTheme;
        }

        // Set cookie with 1 year expiration
        context.Response.Cookies.Append(ThemeCookieName, theme, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = false, // Allow JS to read if needed
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Path = "/"
        });

        return Results.Ok(new ThemeResponse { Theme = theme });
    }

    private static IResult GetTheme(HttpContext context)
    {
        var theme = GetCurrentTheme(context);
        return Results.Ok(new ThemeResponse { Theme = theme });
    }

    /// <summary>
    /// Gets the current theme from the cookie, defaulting to "dark" if not set.
    /// </summary>
    public static string GetCurrentTheme(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(ThemeCookieName, out var theme) &&
            (theme == "dark" || theme == "light"))
        {
            return theme;
        }

        return DefaultTheme;
    }
}

public sealed class ThemeRequest
{
    public string? Theme { get; set; }
}

public sealed class ThemeResponse
{
    public string Theme { get; set; } = ThemeEndpoint.DefaultTheme;
}
