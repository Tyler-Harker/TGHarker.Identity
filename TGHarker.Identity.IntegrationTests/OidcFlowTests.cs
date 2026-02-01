using Microsoft.Playwright;
using TGHarker.Identity.Abstractions.Models;
using TGHarker.Identity.IntegrationTests.Infrastructure;
using Xunit;

namespace TGHarker.Identity.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class OidcFlowTests
{
    private readonly IntegrationTestFixture _fixture;

    public OidcFlowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SimpleLogin_ReturnsAuthorizationCode_WithCorrectState()
    {
        // Arrange
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-{Guid.NewGuid():N}",
            name: "Test Tenant");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"]);

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        // Capture the redirect URL from request events
        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        // Enable request/response logging for debugging and capture the callback URL
        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        // Act
        // Navigate to authorization endpoint
        await page.GotoAsync(authRequest.Url);

        // Should be redirected to login page
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });
        Console.WriteLine($"Login page URL: {page.Url}");

        // Fill in login form
        await page.FillAsync("input[name='Input.Email']", user.Email, new() { Timeout = 5000 });
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Wait for the redirect to be captured
        try
        {
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            // Check if we stayed on login page (error case)
            if (page.Url.Contains("/login"))
            {
                var errorContent = await page.ContentAsync();
                Console.WriteLine($"Login failed, stayed on login page. Content:\n{errorContent}");
            }
            Console.WriteLine($"Timeout waiting for redirect. Current URL: {page.Url}");
            throw;
        }

        // Assert
        Assert.NotNull(finalUrl);
        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);

        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task LoginWithOrganizationSetup_ReturnsAuthorizationCode_WithCorrectState()
    {
        // Arrange - This is the scenario that's currently failing
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-org-{Guid.NewGuid():N}",
            name: "Test Tenant with Org");

        // Create client that requires organization
        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-org-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true,
                OrganizationNameLabel = "Company Name"
            });

        // Create user WITHOUT an organization
        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-noorg-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        // Capture the redirect URL from request events
        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        // Enable request/response logging for debugging and capture the callback URL
        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        // Act
        Console.WriteLine($"Starting auth request with state: {authRequest.State}");
        Console.WriteLine($"Auth URL: {authRequest.Url}");

        // Navigate to authorization endpoint
        await page.GotoAsync(authRequest.Url);

        // Should be redirected to login page
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });
        Console.WriteLine($"Login page URL: {page.Url}");

        // Fill in login form
        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Should be redirected to organization setup page
        await page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 10000 });
        Console.WriteLine($"Setup organization page URL: {page.Url}");

        // Verify state parameter is in the hidden fields
        var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
        Console.WriteLine($"State field value: {stateField}");
        Assert.Equal(authRequest.State, stateField);

        // Fill in organization form
        await page.FillAsync("input[name='Input.OrganizationName']", "My Test Organization");
        await page.ClickAsync("button[type='submit']");

        // Wait for redirect back to client
        try
        {
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"Timeout waiting for redirect. Current URL: {page.Url}");
            var content = await page.ContentAsync();
            Console.WriteLine($"Page content: {content[..Math.Min(2000, content.Length)]}");
            throw;
        }

        // Assert
        Assert.NotNull(finalUrl);
        Console.WriteLine($"Final URL: {finalUrl}");

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);

        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task LoginWithExistingOrganization_ReturnsAuthorizationCode_WithCorrectState()
    {
        // Arrange
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-existing-org-{Guid.NewGuid():N}",
            name: "Test Tenant with Existing Org");

        // Create client that requires organization
        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-existing-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true
            });

        // Create user WITH an existing organization
        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-withorg-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        // Get user ID from the user state
        var userId = user.Id;
        await _fixture.DataBuilder.CreateOrganizationAsync(
            tenantId: tenant.Id,
            userId: userId,
            name: "Existing Organization");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        // Capture the redirect URL from request events
        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        // Act
        await page.GotoAsync(authRequest.Url);

        // Should be redirected to login page
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        // Fill in login form
        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Wait for redirect to client or organization selection
        var waitForSelection = page.WaitForURLAsync(url => url.Contains("/select-organization"), new() { Timeout = 2000 });
        var waitForRedirect = redirectTcs.Task;

        var completed = await Task.WhenAny(waitForSelection, waitForRedirect);

        // If we're at organization selection, select and continue
        if (page.Url.Contains("/select-organization"))
        {
            Console.WriteLine("At organization selection page");
            // Select the first (only) organization
            await page.ClickAsync("input[type='radio']");
            await page.ClickAsync("button[type='submit']");

            // Wait for redirect after selection
            try
            {
                finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"Timeout after org selection. Current URL: {page.Url}");
                throw;
            }
        }
        else if (finalUrl == null)
        {
            // Wait for redirect from direct flow
            try
            {
                finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"Timeout waiting for redirect. Current URL: {page.Url}");
                throw;
            }
        }

        // Assert
        Assert.NotNull(finalUrl);
        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);

        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task OrganizationSetup_VerifyAllOAuthParamsPreserved()
    {
        // This test specifically traces all OAuth parameters through the setup flow
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-trace-{Guid.NewGuid():N}",
            name: "Test Tenant Trace");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-trace-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-trace-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();
        var postDataCaptures = new List<(string Url, string? PostData)>();

        // Capture POST data to verify form submissions
        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Method == "POST")
            {
                postDataCaptures.Add((request.Url, request.PostData));
                Console.WriteLine($"   POST Data: {request.PostData}");
            }
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        Console.WriteLine("=== OAuth Parameters ===");
        Console.WriteLine($"State: {authRequest.State}");
        Console.WriteLine($"Nonce: {authRequest.Nonce}");
        Console.WriteLine($"Code Challenge: {authRequest.CodeChallenge}");

        // Navigate to authorization endpoint
        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        // Login
        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Wait for setup-organization page
        await page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 10000 });

        // Verify all OAuth params are in the URL
        var setupUrl = page.Url;
        Console.WriteLine($"\n=== Setup Organization URL ===");
        Console.WriteLine(setupUrl);

        Assert.Contains($"client_id={client.ClientId}", setupUrl);
        Assert.Contains($"state={authRequest.State}", setupUrl);
        Assert.Contains($"nonce={authRequest.Nonce}", setupUrl);
        Assert.Contains($"code_challenge={Uri.EscapeDataString(authRequest.CodeChallenge)}", setupUrl);

        // Verify all hidden fields have the correct values
        Console.WriteLine("\n=== Hidden Field Values ===");
        var clientIdField = await page.Locator("input[name='ClientId']").GetAttributeAsync("value");
        var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
        var nonceField = await page.Locator("input[name='Nonce']").GetAttributeAsync("value");
        var codeChallengeField = await page.Locator("input[name='CodeChallenge']").GetAttributeAsync("value");
        var redirectUriField = await page.Locator("input[name='RedirectUri']").GetAttributeAsync("value");

        Console.WriteLine($"ClientId field: {clientIdField}");
        Console.WriteLine($"State field: {stateField}");
        Console.WriteLine($"Nonce field: {nonceField}");
        Console.WriteLine($"CodeChallenge field: {codeChallengeField}");
        Console.WriteLine($"RedirectUri field: {redirectUriField}");

        Assert.Equal(client.ClientId, clientIdField);
        Assert.Equal(authRequest.State, stateField);
        Assert.Equal(authRequest.Nonce, nonceField);
        Assert.Equal(authRequest.CodeChallenge, codeChallengeField);
        Assert.Equal("https://localhost:5001/signin-oidc", redirectUriField);

        // Fill in organization form
        await page.FillAsync("input[name='Input.OrganizationName']", "Traced Organization");
        await page.ClickAsync("button[type='submit']");

        // Wait for redirect
        finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Verify POST data included OAuth params
        Console.WriteLine("\n=== POST Data Analysis ===");
        var setupPost = postDataCaptures.FirstOrDefault(p => p.Url.Contains("/setup-organization"));
        if (setupPost.PostData != null)
        {
            Console.WriteLine($"Setup POST data: {setupPost.PostData}");
            Assert.Contains("State=", setupPost.PostData);
            Assert.Contains("ClientId=", setupPost.PostData);
        }

        // Final assertions
        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Console.WriteLine($"\n=== Final Response ===");
        Console.WriteLine($"Code: {response.Code}");
        Console.WriteLine($"State: {response.State}");
        Console.WriteLine($"Expected State: {authRequest.State}");

        Assert.True(response.IsSuccess);
        Assert.Equal(authRequest.State, response.State);
    }

    [Fact]
    public async Task OrganizationSetup_WithSpecialCharsInState_PreservesState()
    {
        // Test with a state value that contains URL-special characters
        // Some OIDC clients use Base64 or other encodings that may contain +, /, =
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-special-{Guid.NewGuid():N}",
            name: "Test Tenant Special");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-special-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-special-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        // Use a state with special characters (like Base64 might produce)
        var specialState = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        Console.WriteLine($"Special state (Base64): {specialState}");

        var authRequest = oidcClient.BuildAuthorizationRequest(state: specialState);

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        await page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 10000 });

        // Verify state is preserved in hidden field
        var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
        Console.WriteLine($"State field value: {stateField}");
        Assert.Equal(specialState, stateField);

        await page.FillAsync("input[name='Input.OrganizationName']", "Special Org");
        await page.ClickAsync("button[type='submit']");

        finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Console.WriteLine($"Response state: {response.State}");
        Console.WriteLine($"Expected state: {specialState}");

        Assert.True(response.IsSuccess);
        Assert.Equal(specialState, response.State);
    }

    [Fact]
    public async Task OrganizationSelection_WithMultipleOrgs_PreservesState()
    {
        // Test the select-organization flow with multiple organizations
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-multiorg-{Guid.NewGuid():N}",
            name: "Test Tenant Multi Org");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-multiorg-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-multiorg-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        // Create two organizations for this user
        await _fixture.DataBuilder.CreateOrganizationAsync(
            tenantId: tenant.Id,
            userId: user.Id,
            name: "First Organization",
            identifier: $"org1-{Guid.NewGuid():N}");

        await _fixture.DataBuilder.CreateOrganizationAsync(
            tenantId: tenant.Id,
            userId: user.Id,
            name: "Second Organization",
            identifier: $"org2-{Guid.NewGuid():N}");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        Console.WriteLine($"Auth state: {authRequest.State}");

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Should go to organization selection (user has multiple orgs)
        await page.WaitForURLAsync(url => url.Contains("/select-organization"), new() { Timeout = 10000 });

        Console.WriteLine($"Select organization URL: {page.Url}");

        // Verify state is in the URL
        Assert.Contains($"state={authRequest.State}", page.Url);

        // Verify hidden fields (if they exist on this page)
        var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
        Console.WriteLine($"State field: {stateField}");
        Assert.Equal(authRequest.State, stateField);

        // Select the first organization
        await page.Locator("input[type='radio']").First.ClickAsync();
        await page.ClickAsync("button[type='submit']");

        finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Console.WriteLine($"Final state: {response.State}");

        Assert.True(response.IsSuccess);
        Assert.Equal(authRequest.State, response.State);
    }

    [Fact]
    public async Task OrganizationSetup_RefreshPage_PreservesState()
    {
        // Test that refreshing the setup-organization page preserves OAuth params
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-refresh-{Guid.NewGuid():N}",
            name: "Test Tenant Refresh");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-refresh-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-refresh-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        await page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 10000 });

        // Capture the URL before refresh
        var setupUrl = page.Url;
        Console.WriteLine($"Setup URL before refresh: {setupUrl}");

        // Refresh the page
        await page.ReloadAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        Console.WriteLine($"Setup URL after refresh: {page.Url}");

        // Verify state is still in hidden field after refresh
        var stateFieldAfterRefresh = await page.Locator("input[name='State']").GetAttributeAsync("value");
        Console.WriteLine($"State field after refresh: {stateFieldAfterRefresh}");
        Assert.Equal(authRequest.State, stateFieldAfterRefresh);

        // Complete the flow
        await page.FillAsync("input[name='Input.OrganizationName']", "Refreshed Org");
        await page.ClickAsync("button[type='submit']");

        finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Assert.True(response.IsSuccess);
        Assert.Equal(authRequest.State, response.State);
    }

    [Fact]
    public async Task OrganizationMode_AutoCreate_CreatesOrgAutomatically_PreservesState()
    {
        // Test OrganizationRegistrationMode.AutoCreate - organization is auto-created
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-autocreate-{Guid.NewGuid():N}",
            name: "Test Tenant AutoCreate");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-autocreate-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.AutoCreate,
                DefaultOrganizationRole = WellKnownOrganizationRoles.Owner
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-autocreate-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        Console.WriteLine($"Auth state: {authRequest.State}");

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // AutoCreate mode should auto-create org and redirect to setup page with auto-submit
        // Wait for either setup-organization (auto-submit) or direct redirect
        var waitResult = await Task.WhenAny(
            page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 5000 }).ContinueWith(_ => "setup"),
            redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(_ => "redirect")
        );

        var result = await waitResult;
        Console.WriteLine($"Wait result: {result}");

        if (result == "setup" && page.Url.Contains("/setup-organization"))
        {
            Console.WriteLine($"Setup organization page URL: {page.Url}");

            // Verify state is preserved
            var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
            Console.WriteLine($"State field: {stateField}");
            Assert.Equal(authRequest.State, stateField);

            // In AutoCreate mode, org name is not required - submit without filling
            // The page may auto-submit or allow submit without name
            var orgNameInput = page.Locator("input[name='Input.OrganizationName']");
            if (await orgNameInput.IsVisibleAsync())
            {
                // Fill if visible (might be optional in AutoCreate mode)
                await orgNameInput.FillAsync("Auto Created Org");
            }

            await page.ClickAsync("button[type='submit']");
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        else if (finalUrl == null)
        {
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        // Assert
        Assert.NotNull(finalUrl);
        Console.WriteLine($"Final URL: {finalUrl}");

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task OrganizationMode_OptionalPrompt_AllowsSkippingOrg_PreservesState()
    {
        // Test OrganizationRegistrationMode.OptionalPrompt - user can optionally create org
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-optional-{Guid.NewGuid():N}",
            name: "Test Tenant Optional");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-optional-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.OptionalPrompt,
                RequireOrganizationName = false // Not required in optional mode
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-optional-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        Console.WriteLine($"Auth state: {authRequest.State}");

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Wait for setup-organization or direct redirect
        var waitResult = await Task.WhenAny(
            page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 5000 }).ContinueWith(_ => "setup"),
            redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(_ => "redirect")
        );

        var result = await waitResult;
        Console.WriteLine($"Wait result: {result}");

        if (result == "setup" && page.Url.Contains("/setup-organization"))
        {
            Console.WriteLine($"Setup organization page URL: {page.Url}");

            // Verify state is preserved
            var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
            Console.WriteLine($"State field: {stateField}");
            Assert.Equal(authRequest.State, stateField);

            // In OptionalPrompt mode, we can skip org creation by submitting without filling name
            // Just submit the form without filling the optional org name
            await page.ClickAsync("button[type='submit']");
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        else if (finalUrl == null)
        {
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        // Assert
        Assert.NotNull(finalUrl);
        Console.WriteLine($"Final URL: {finalUrl}");

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task OrganizationMode_OptionalPrompt_WithOrgCreation_PreservesState()
    {
        // Test OrganizationRegistrationMode.OptionalPrompt - user chooses to create org
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-optional-org-{Guid.NewGuid():N}",
            name: "Test Tenant Optional Org");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-optional-org-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.OptionalPrompt,
                RequireOrganizationName = false,
                OrganizationNameLabel = "Company Name (Optional)"
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-optional-org-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        Console.WriteLine($"Auth state: {authRequest.State}");

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Wait for setup-organization or direct redirect
        var waitResult = await Task.WhenAny(
            page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 5000 }).ContinueWith(_ => "setup"),
            redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10)).ContinueWith(_ => "redirect")
        );

        var result = await waitResult;
        Console.WriteLine($"Wait result: {result}");

        if (result == "setup" && page.Url.Contains("/setup-organization"))
        {
            Console.WriteLine($"Setup organization page URL: {page.Url}");

            // Verify state is preserved
            var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
            Console.WriteLine($"State field: {stateField}");
            Assert.Equal(authRequest.State, stateField);

            // In OptionalPrompt mode, user chooses to create an org
            var orgNameInput = page.Locator("input[name='Input.OrganizationName']");
            if (await orgNameInput.IsVisibleAsync())
            {
                await orgNameInput.FillAsync("My Optional Organization");
            }

            await page.ClickAsync("button[type='submit']");
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        else if (finalUrl == null)
        {
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        // Assert
        Assert.NotNull(finalUrl);
        Console.WriteLine($"Final URL: {finalUrl}");

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task OrganizationMode_None_NoOrgRequired_DirectLogin()
    {
        // Test OrganizationRegistrationMode.None explicitly - no org required at all
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-noorg-{Guid.NewGuid():N}",
            name: "Test Tenant No Org");

        // Create client with OrganizationsEnabled = false (None mode)
        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-noorg-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = false, // Explicitly disabled
                OrganizationMode = OrganizationRegistrationMode.None
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-noorg-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        Console.WriteLine($"Auth state: {authRequest.State}");

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Should go directly to redirect without setup-organization or select-organization
        try
        {
            finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }
        catch (TimeoutException)
        {
            // Check if we ended up somewhere unexpected
            Console.WriteLine($"Timeout. Current URL: {page.Url}");
            if (page.Url.Contains("/setup-organization") || page.Url.Contains("/select-organization"))
            {
                Assert.Fail($"Should not redirect to organization pages when OrganizationsEnabled=false. Current URL: {page.Url}");
            }
            throw;
        }

        // Assert
        Assert.NotNull(finalUrl);
        Console.WriteLine($"Final URL: {finalUrl}");

        // Verify we didn't go through organization pages
        Assert.DoesNotContain("/setup-organization", finalUrl);
        Assert.DoesNotContain("/select-organization", finalUrl);

        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task AlreadyLoggedIn_NoOrg_RedirectsToSetupOrganization_PreservesState()
    {
        // Test the scenario where user is already authenticated but has no organization
        // and then visits /authorize - should redirect to setup-organization
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-already-auth-{Guid.NewGuid():N}",
            name: "Test Tenant Already Auth");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-already-auth-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true
            });

        // Create user WITHOUT an organization
        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-already-auth-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        // First, log in to establish a session
        var initialAuthRequest = oidcClient.BuildAuthorizationRequest();
        Console.WriteLine($"Initial auth state: {initialAuthRequest.State}");

        await page.GotoAsync(initialAuthRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        // Should redirect to setup-organization
        await page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 10000 });
        Console.WriteLine($"First visit setup-organization URL: {page.Url}");

        // Now, simulate the user navigating AWAY and then coming back with a NEW authorize request
        // This simulates the case where user is already logged in but hasn't completed org setup
        var newAuthRequest = oidcClient.BuildAuthorizationRequest();
        Console.WriteLine($"New auth state: {newAuthRequest.State}");

        // Navigate directly to /authorize with a new state (simulating a fresh OAuth flow)
        await page.GotoAsync(newAuthRequest.Url);

        // Since user is already authenticated but has no org, should go to setup-organization
        await page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 10000 });
        Console.WriteLine($"Second visit setup-organization URL: {page.Url}");

        // Verify the NEW state is in the URL
        Assert.Contains($"state={Uri.EscapeDataString(newAuthRequest.State)}", page.Url);

        // Verify state is preserved in hidden field
        var stateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
        Console.WriteLine($"State field: {stateField}");
        Assert.Equal(newAuthRequest.State, stateField);

        // Complete the flow
        await page.FillAsync("input[name='Input.OrganizationName']", "My Organization");
        await page.ClickAsync("button[type='submit']");

        finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert the NEW state is returned (not the old one)
        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(newAuthRequest.State, response.State);
        Assert.NotEmpty(response.Code!);
    }

    [Fact]
    public async Task OrganizationMode_Prompt_RequiredName_ValidationError_PreservesState()
    {
        // Test that validation errors preserve state when org name is required but empty
        var tenant = await _fixture.DataBuilder.CreateTenantAsync(
            identifier: $"test-validation-{Guid.NewGuid():N}",
            name: "Test Tenant Validation");

        var (client, _) = await _fixture.DataBuilder.CreateClientAsync(
            tenantId: tenant.Id,
            clientIdentifier: $"client-validation-{Guid.NewGuid():N}",
            redirectUris: ["https://localhost:5001/signin-oidc"],
            userFlowSettings: new UserFlowSettings
            {
                OrganizationsEnabled = true,
                OrganizationMode = OrganizationRegistrationMode.Prompt,
                RequireOrganizationName = true,
                OrganizationNameLabel = "Company Name"
            });

        var user = await _fixture.DataBuilder.CreateUserAsync(
            tenantId: tenant.Id,
            email: $"user-validation-{Guid.NewGuid():N}@example.com",
            password: "TestPassword123!");

        var oidcClient = new TestOidcClient(
            _fixture.BaseAddress,
            tenant.Identifier,
            client.ClientId,
            "https://localhost:5001/signin-oidc");

        var authRequest = oidcClient.BuildAuthorizationRequest();

        await using var context = await _fixture.CreateBrowserContextAsync();
        var page = await context.NewPageAsync();

        string? finalUrl = null;
        var redirectTcs = new TaskCompletionSource<string>();

        page.Request += (_, request) =>
        {
            Console.WriteLine($">> {request.Method} {request.Url}");
            if (request.Url.StartsWith("https://localhost:5001/"))
            {
                finalUrl = request.Url;
                redirectTcs.TrySetResult(finalUrl);
            }
        };
        page.Response += (_, response) => Console.WriteLine($"<< {response.Status} {response.Url}");

        await page.GotoAsync(authRequest.Url);
        await page.WaitForURLAsync(url => url.Contains("/login"), new() { Timeout = 10000 });

        await page.FillAsync("input[name='Input.Email']", user.Email);
        await page.FillAsync("input[name='Input.Password']", "TestPassword123!");
        await page.ClickAsync("button[type='submit']");

        await page.WaitForURLAsync(url => url.Contains("/setup-organization"), new() { Timeout = 10000 });

        // Verify initial state
        var initialStateField = await page.Locator("input[name='State']").GetAttributeAsync("value");
        Assert.Equal(authRequest.State, initialStateField);

        // Submit without filling org name (should cause validation error)
        await page.ClickAsync("button[type='submit']");

        // Wait for page to reload with error (should stay on same page)
        await page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        // Should still be on setup-organization page
        Assert.Contains("/setup-organization", page.Url);
        Console.WriteLine($"After validation error URL: {page.Url}");

        // Verify state is still preserved after validation error
        var stateAfterError = await page.Locator("input[name='State']").GetAttributeAsync("value");
        Console.WriteLine($"State after validation error: {stateAfterError}");
        Assert.Equal(authRequest.State, stateAfterError);

        // Now fill in the org name and submit again
        await page.FillAsync("input[name='Input.OrganizationName']", "Valid Organization Name");
        await page.ClickAsync("button[type='submit']");

        finalUrl = await redirectTcs.Task.WaitAsync(TimeSpan.FromSeconds(15));

        // Assert
        Assert.NotNull(finalUrl);
        var response = TestOidcClient.ParseAuthorizationResponse(finalUrl);
        Assert.True(response.IsSuccess, $"Expected success but got error: {response.Error} - {response.ErrorDescription}");
        Assert.Equal(authRequest.State, response.State);
    }
}
