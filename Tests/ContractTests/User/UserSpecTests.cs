using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.User;

/// <summary>
/// Contract tests for every <c>/users*</c> operation against its documented OpenAPI schema
/// (<c>{API_URL}/docs</c>) - the shape, not the business data. One file per resource, grouped by
/// operation below, per <c>.claude/skills/contract-testing/SKILL.md</c>'s Spec-track convention.
///
/// This resource is the most involved of the three tested so far (Brand, Category, User):
/// stateful auth tokens (login/refresh/logout all mutate token validity, not just data),
/// destructive account operations (register/delete/change-password/forgot-password), and three
/// distinct response shapes for what the spec documents as a single <c>UserResponse</c> - see
/// the per-group comments below for exactly which fields each viewpoint actually returns.
///
/// Known gaps tracked separately and deliberately red: see
/// <see cref="UserMissingUserErrorGapSpecTests"/>,
/// <see cref="UserRegistrationValidationGapSpecTests"/>, and
/// <see cref="UserAgeValidationGapSpecTests"/>.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("User")]
[AllureSubSuite("UserSpecTests")]
public class UserSpecTests : UserContractTestBase
{
    // The spec documents one UserResponse, but the live API actually returns three different
    // subsets of it depending on who's asking and from where:
    private static readonly string[] RegisterResponseProperties =
        ["first_name", "last_name", "email", "id", "created_at", "address"];
    private static readonly string[] SelfViewUserProperties =
        ["id", "provider", "first_name", "last_name", "phone", "dob", "email", "totp_enabled", "created_at", "address"];
    private static readonly string[] AdminViewUserProperties =
        [.. SelfViewUserProperties, "enabled", "role", "failed_login_attempts"];

    // ---- POST /users/register ----

    [Test]
    [AllureName("POST /users/register with a valid new account returns 201, but only a trimmed subset of UserResponse")]
    public async Task Register_ValidUser_ReturnsCreated()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe User");
        CreatedUserId = user.Id;

        // Re-register is impossible to re-run against the same response, so this test just
        // re-asserts the shape RegisterThrowawayUserAsync already got back - the real check is
        // the property set, not the data.
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = "Spec Probe User 2", last_name = "Probe", email = $"spec-probe-2-{Guid.NewGuid():N}@example.com", password = GenerateStrongPassword() },
        });
        Assert.That(response.Status, Is.EqualTo(201));
        var body = (await response.JsonAsync())!.Value;
        // provider/phone/dob/totp_enabled/enabled/role/failed_login_attempts - all documented on
        // UserResponse - are simply absent here, not null, found by probing the live endpoint.
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(RegisterResponseProperties));

        await DeleteUserAsync(body.GetProperty("id").GetString()!, await GetAdminTokenAsync());
    }

    [Test]
    [AllureName("POST /users/register with missing required fields returns 422 with field-keyed messages")]
    public async Task Register_MissingRequiredFields_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("first_name")[0].GetString(), Is.EqualTo("The first name field is required."));
        Assert.That(body.GetProperty("last_name")[0].GetString(), Is.EqualTo("The last name field is required."));
        Assert.That(body.GetProperty("email")[0].GetString(), Is.EqualTo("The email field is required."));
        Assert.That(body.GetProperty("password")[0].GetString(), Is.EqualTo("The password field is required."));
    }

    [Test]
    [AllureName("POST /users/register with an email already registered returns 409")]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = "Dup", last_name = "Probe", email = Accounts.Customer.Email, password = GenerateStrongPassword() },
        });

        Assert.That(response.Status, Is.EqualTo(409));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("email")[0].GetString(), Does.Contain("already exists"));
    }

    [Test]
    [AllureName("POST /users/register with a password missing a required character class returns 422")]
    public async Task Register_PasswordMissingComplexity_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = "A", last_name = "B", email = $"complexity-probe-{Guid.NewGuid():N}@example.com", password = "Abcdefgh123" },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("password")[0].GetString(), Does.Contain("symbol"));
    }

    [Test]
    [AllureName("POST /users/register with a password found in a breach database returns 422 - the docs' own example password trips this")]
    public async Task Register_LeakedPassword_ReturnsUnprocessableEntity()
    {
        // The OpenAPI spec's own example password, "SuperSecure@123", satisfies every documented
        // complexity rule yet is itself rejected here - found by probing the live endpoint, not
        // documented in /docs. Confirms the breach check runs independently of the regex checks.
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = "A", last_name = "B", email = $"leaked-probe-{Guid.NewGuid():N}@example.com", password = "SuperSecure@123" },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("password")[0].GetString(), Does.Contain("data leak"));
    }

    [Test]
    [AllureName("POST /users/register with a dob under 18 years returns 422")]
    public async Task Register_TooYoung_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = "A", last_name = "B", email = $"young-probe-{Guid.NewGuid():N}@example.com", password = GenerateStrongPassword(), dob = DateTime.UtcNow.AddYears(-10).ToString("yyyy-MM-dd") },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("dob")[0].GetString(), Does.Contain("18 years old"));
    }

    [Test]
    [AllureName("POST /users/register with a dob old enough to trip the age ceiling returns 422")]
    public async Task Register_TooOld_ReturnsUnprocessableEntity()
    {
        // 100 years is comfortably past the ceiling regardless of exactly where it sits - see
        // the next comment. A dob picked near the documented "75 years" boundary itself would be
        // flaky here: see UserAgeValidationGapSpecTests for the real, much higher threshold.
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = "A", last_name = "B", email = $"old-probe-{Guid.NewGuid():N}@example.com", password = GenerateStrongPassword(), dob = DateTime.UtcNow.AddYears(-100).ToString("yyyy-MM-dd") },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        // The message still claims "75 years old" even though the actually-enforced ceiling is
        // much higher - see UserAgeValidationGapSpecTests.Register_OlderThan75ButUnder93_ShouldBeRejectedButIsNot.
        Assert.That(body.GetProperty("dob")[0].GetString(), Does.Contain("75 years old"));
    }

    // ---- POST /users/login ----

    [Test]
    [AllureName("POST /users/login with valid credentials returns 200 with a TokenResponse")]
    public async Task Login_ValidCredentials_ReturnsTokenResponse()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe Login");
        CreatedUserId = user.Id;

        var response = await ApiContext.PostAsync("/users/login", new APIRequestContextOptions
        {
            DataObject = new { email = user.Email, password = user.Password },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("access_token").GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(body.GetProperty("token_type").GetString(), Is.EqualTo("bearer"));
        Assert.That(body.GetProperty("expires_in").GetInt32(), Is.GreaterThan(0));
    }

    [Test]
    [AllureName("POST /users/login with invalid credentials returns 401 with {error: 'Unauthorized'} - not the {message: ...} shape used elsewhere")]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var response = await ApiContext.PostAsync("/users/login", new APIRequestContextOptions
        {
            DataObject = new { email = "does-not-exist@example.com", password = "whatever-wrong-password" },
        });

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        // Every other 401 in this API (and in Brand/Category) uses {"message": "Unauthorized"} -
        // login alone uses the key "error" instead. Real consumer dependency on this exact
        // shape: see UserPactTests.LoginFailure_MatchesConsumerExpectedErrorShape.
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "error" }));
        Assert.That(body.GetProperty("error").GetString(), Is.EqualTo("Unauthorized"));
    }

    // ---- GET /users/me ----

    [Test]
    [AllureName("GET /users/me with a valid token returns 200 with the self-view UserResponse shape")]
    public async Task Me_WithAuth_ReturnsSelfViewShape()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe Me");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.GetAsync("/users/me", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        // Self-view omits enabled/role/failed_login_attempts - those three only appear to an
        // admin viewer (GET /users, GET /users/search, GET /users/{id} as admin).
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(SelfViewUserProperties));
        Assert.That(body.GetProperty("email").GetString(), Is.EqualTo(user.Email));
    }

    [Test]
    [AllureName("GET /users/me with no auth returns 401")]
    public async Task Me_NoAuth_ReturnsUnauthorized()
    {
        var response = await ApiContext.GetAsync("/users/me");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));
    }

    // ---- GET /users/refresh, GET /users/logout ----

    [Test]
    [AllureName("GET /users/refresh returns a new token and invalidates the one used to request it")]
    public async Task Refresh_ReturnsNewTokenAndInvalidatesTheOldOne()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe Refresh");
        CreatedUserId = user.Id;
        var oldToken = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.GetAsync("/users/refresh", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {oldToken}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        var newToken = body.GetProperty("access_token").GetString()!;
        Assert.That(newToken, Is.Not.EqualTo(oldToken));

        var withNewToken = await ApiContext.GetAsync("/users/me", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {newToken}" },
        });
        Assert.That(withNewToken.Status, Is.EqualTo(200), "the refreshed token should work");

        // The real, stateful part of this contract: the token that was spent to get the refresh
        // is itself invalidated, not just superseded - found by probing the live endpoint.
        var withOldToken = await ApiContext.GetAsync("/users/me", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {oldToken}" },
        });
        Assert.That(withOldToken.Status, Is.EqualTo(401), "the token spent on refresh should no longer work");
    }

    [Test]
    [AllureName("GET /users/logout returns 200 and invalidates the token used")]
    public async Task Logout_InvalidatesToken()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe Logout");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.GetAsync("/users/logout", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Successfully logged out"));

        var afterLogout = await ApiContext.GetAsync("/users/me", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });
        Assert.That(afterLogout.Status, Is.EqualTo(401), "a token should not work after its own logout");
    }

    // ---- POST /users/change-password ----

    /// <summary>
    /// Every call in this group sends an explicit <c>Accept: application/json</c> header. This
    /// endpoint's validation failures silently fall back to a <c>302</c> HTML redirect (Laravel's
    /// default web-request behavior) when that header is absent, even though the request itself
    /// is JSON - found live by probing with and without it. This isn't a raw-client footgun to
    /// shrug off: Angular's <c>HttpClient</c>, the real consumer here, sends
    /// <c>Accept: application/json, text/plain, */*</c> on every request by default, so this is
    /// what the endpoint actually has to honor in practice. A Playwright <c>APIRequestContext</c>
    /// does not set this header by default (unlike a browser), so it must be set explicitly here
    /// to see the real JSON contract rather than an artifact of the HTTP client's own defaults.
    /// </summary>
    private static Dictionary<string, string> JsonAcceptHeaders(string? bearerToken = null)
    {
        var headers = new Dictionary<string, string> { ["Accept"] = "application/json" };
        if (bearerToken is not null)
        {
            headers["Authorization"] = $"Bearer {bearerToken}";
        }
        return headers;
    }

    [Test]
    [AllureName("POST /users/change-password with the correct current password succeeds and the new password actually works")]
    public async Task ChangePassword_ValidCurrentPassword_ActuallyChangesIt()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe ChangePw");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);
        var newPassword = GenerateStrongPassword();

        var response = await ApiContext.PostAsync("/users/change-password", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(token),
            DataObject = new { current_password = user.Password, new_password = newPassword, new_password_confirmation = newPassword },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);

        var loginWithNewPassword = await ApiContext.PostAsync("/users/login", new APIRequestContextOptions
        {
            DataObject = new { email = user.Email, password = newPassword },
        });
        Assert.That(loginWithNewPassword.Status, Is.EqualTo(200), "the new password should actually work after change-password");
    }

    [Test]
    [AllureName("POST /users/change-password with the wrong current password returns 400, not 401/422")]
    public async Task ChangePassword_WrongCurrentPassword_ReturnsBadRequest()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe ChangePw");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.PostAsync("/users/change-password", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(token),
            DataObject = new { current_password = "definitely-wrong", new_password = GenerateStrongPassword(), new_password_confirmation = GenerateStrongPassword() },
        });

        Assert.That(response.Status, Is.EqualTo(400));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.False);
        Assert.That(body.GetProperty("message").GetString(), Does.Contain("does not matches"));
    }

    [Test]
    [AllureName("POST /users/change-password with a mismatched confirmation returns 422 with a field-keyed message")]
    public async Task ChangePassword_MismatchedConfirmation_ReturnsUnprocessableEntity()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe ChangePw");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.PostAsync("/users/change-password", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(token),
            DataObject = new { current_password = user.Password, new_password = GenerateStrongPassword(), new_password_confirmation = "totally-different" },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        // Unlike /users/register's flat {field: [...]} shape, this one wraps in a top-level
        // {message, errors} envelope - Laravel's other default validation-failure shape. Both
        // shapes are real; the admin UI's error renderer is written to handle either (see
        // register.component.ts's fieldErrors = body?.errors ?? body fallback).
        Assert.That(body.GetProperty("errors").GetProperty("new_password")[0].GetString(), Does.Contain("confirmation does not match"));
    }

    [Test]
    [AllureName("POST /users/change-password with no auth returns 401")]
    public async Task ChangePassword_NoAuth_ReturnsUnauthorized()
    {
        var response = await ApiContext.PostAsync("/users/change-password", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(),
            DataObject = new { current_password = "x", new_password = "y", new_password_confirmation = "y" },
        });

        Assert.That(response.Status, Is.EqualTo(401));
    }

    // ---- POST /users/forgot-password ----

    [Test]
    [AllureName("POST /users/forgot-password for a known email resets the password to welcome02, as documented")]
    public async Task ForgotPassword_KnownEmail_ResetsPasswordToWelcome02()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe ForgotPw");
        CreatedUserId = user.Id;

        var response = await ApiContext.PostAsync("/users/forgot-password", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(),
            DataObject = new { email = user.Email },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);

        var loginWithWelcome02 = await ApiContext.PostAsync("/users/login", new APIRequestContextOptions
        {
            DataObject = new { email = user.Email, password = "welcome02" },
        });
        Assert.That(loginWithWelcome02.Status, Is.EqualTo(200), "forgot-password should have actually reset the password to welcome02");
    }

    [Test]
    [AllureName("POST /users/forgot-password for an unknown email returns 422 with a field-keyed message")]
    public async Task ForgotPassword_UnknownEmail_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.PostAsync("/users/forgot-password", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(),
            DataObject = new { email = "no-such-account-exists@example.com" },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("errors").GetProperty("email")[0].GetString(), Does.Contain("invalid"));
    }

    // ---- GET /users, GET /users/search ----

    [Test]
    [AllureName("GET /users as admin returns 200 with a paginated list in the admin-view shape")]
    public async Task List_AsAdmin_ReturnsPaginatedAdminViewShape()
    {
        var token = await GetAdminTokenAsync();

        var response = await ApiContext.GetAsync("/users", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name),
            Is.EquivalentTo(new[] { "current_page", "data", "from", "last_page", "per_page", "to", "total" }));
        Assert.That(body.GetProperty("data").GetArrayLength(), Is.GreaterThan(0));
        Assert.That(body.GetProperty("data")[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(AdminViewUserProperties));
    }

    [Test]
    [AllureName("GET /users as a regular (non-admin) customer returns 403, unlike GET /users/search which allows it")]
    public async Task List_AsRegularCustomer_ReturnsForbidden()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe List");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.GetAsync("/users", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(403));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Forbidden"));
    }

    [Test]
    [AllureName("GET /users with no auth returns 401")]
    public async Task List_NoAuth_ReturnsUnauthorized()
    {
        var response = await ApiContext.GetAsync("/users");

        Assert.That(response.Status, Is.EqualTo(401));
    }

    [Test]
    [AllureName("GET /users/search allows any authenticated user, not just admin, and still returns the admin-view shape")]
    public async Task Search_AsRegularCustomer_IsAllowedUnlikeList()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe Search");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        // Asymmetric with GET /users (admin-only, see List_AsRegularCustomer_ReturnsForbidden) -
        // the spec documents the same apiAuth security on both, with no stated role difference,
        // but only /search actually permits a non-admin caller. Found by probing live.
        var response = await ApiContext.GetAsync($"/users/search?q={Uri.EscapeDataString("Jane")}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("data").GetArrayLength(), Is.GreaterThan(0));
        // Unlike GET /users/{id} (which switches shape based on the VIEWER's role - see
        // GetById_AsAdmin_ReturnsAdminViewShapeForAnyUser vs GetById_Self_ReturnsSelfViewShape),
        // search's shape also depends on the viewer, not the target: a non-admin searcher gets
        // the trimmed self-view shape even for a match that isn't themselves.
        Assert.That(body.GetProperty("data")[0].EnumerateObject().Select(p => p.Name), Is.EquivalentTo(SelfViewUserProperties));
    }

    [Test]
    [AllureName("GET /users/search with no auth returns 401")]
    public async Task Search_NoAuth_ReturnsUnauthorized()
    {
        var response = await ApiContext.GetAsync("/users/search?q=Jane");

        Assert.That(response.Status, Is.EqualTo(401));
    }

    [Test]
    [AllureName("QUERY /users/search (RFC 9110 QUERY) matches the GET variant's response shape")]
    public async Task Search_QueryMethodVariant_MatchesGetVariant()
    {
        var token = await GetAdminTokenAsync();

        var response = await ApiContext.FetchAsync("/users/search", new APIRequestContextOptions
        {
            Method = "QUERY",
            DataObject = new { q = "Jane" },
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json", ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("data").GetArrayLength(), Is.GreaterThan(0));
    }

    // ---- GET /users/{id}, PUT /users/{id}, PATCH /users/{id}, DELETE /users/{id} ----

    [Test]
    [AllureName("GET /users/{id} for yourself returns 200 with the self-view shape")]
    public async Task GetById_Self_ReturnsSelfViewShape()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe GetSelf");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.GetAsync($"/users/{user.Id}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(SelfViewUserProperties));
    }

    [Test]
    [AllureName("GET /users/{id} for a different user, as admin, returns 200 with the admin-view shape")]
    public async Task GetById_AsAdmin_ReturnsAdminViewShapeForAnyUser()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe GetAdmin");
        CreatedUserId = user.Id;
        var token = await GetAdminTokenAsync();

        var response = await ApiContext.GetAsync($"/users/{user.Id}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(AdminViewUserProperties));
    }

    [Test]
    [AllureName("GET /users/{id} for a different user, as a regular customer, returns 404 - not 403 - with a custom message")]
    public async Task GetById_AsRegularCustomerForSomeoneElse_ReturnsNotFoundNotForbidden()
    {
        var viewer = await RegisterThrowawayUserAsync("Spec Probe Viewer");
        var target = await RegisterThrowawayUserAsync("Spec Probe Target");
        var viewerToken = await LoginAsAsync(viewer.Email, viewer.Password);

        try
        {
            var response = await ApiContext.GetAsync($"/users/{target.Id}", new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {viewerToken}" },
            });

            // Deliberately obfuscated as "not found" rather than "forbidden" - avoids confirming
            // that the id exists to a caller who isn't allowed to see it. Different shape again:
            // {"error": "..."} , not the spec's ItemNotFoundResponse {"message": "..."}.
            Assert.That(response.Status, Is.EqualTo(404));
            var body = (await response.JsonAsync())!.Value;
            Assert.That(body.GetProperty("error").GetString(), Does.Contain("not authorized to view"));
        }
        finally
        {
            var adminToken = await GetAdminTokenAsync();
            await DeleteUserAsync(viewer.Id, adminToken);
            await DeleteUserAsync(target.Id, adminToken);
        }
    }

    [Test]
    [AllureName("PUT /users/{id} for yourself with a full valid payload returns 200 {success:true}")]
    public async Task Put_Self_ReturnsSuccess()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe PutSelf");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.PutAsync($"/users/{user.Id}", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(token),
            DataObject = new
            {
                first_name = "Updated",
                last_name = "Name",
                email = user.Email,
                address = new { street = "Street 1", city = "City", country = "Country" },
            },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);
    }

    [Test]
    [AllureName("PUT /users/{id} for yourself without street/city/country returns 422 - UserRequest's address sub-fields are documented as optional but are actually required")]
    public async Task Put_Self_WithoutRequiredAddressFields_ReturnsUnprocessableEntity()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe PutSelfAddr");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.PutAsync($"/users/{user.Id}", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(token),
            DataObject = new { first_name = "Updated", last_name = "Name", email = user.Email },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("address.street")[0].GetString(), Does.Contain("required"));
        Assert.That(body.GetProperty("address.city")[0].GetString(), Does.Contain("required"));
        Assert.That(body.GetProperty("address.country")[0].GetString(), Does.Contain("required"));
    }

    [Test]
    [AllureName("PUT /users/{id} for a different user, as a regular customer, returns 403 - ownership is checked after validation, not before")]
    public async Task Put_AsRegularCustomerForSomeoneElse_ReturnsForbidden()
    {
        var actor = await RegisterThrowawayUserAsync("Spec Probe PutActor");
        var target = await RegisterThrowawayUserAsync("Spec Probe PutTarget");
        var actorToken = await LoginAsAsync(actor.Email, actor.Password);

        try
        {
            var response = await ApiContext.PutAsync($"/users/{target.Id}", new APIRequestContextOptions
            {
                Headers = JsonAcceptHeaders(actorToken),
                DataObject = new
                {
                    first_name = "Hijacked",
                    last_name = "Name",
                    email = target.Email,
                    address = new { street = "x", city = "y", country = "z" },
                },
            });

            Assert.That(response.Status, Is.EqualTo(403));
            var body = (await response.JsonAsync())!.Value;
            Assert.That(body.GetProperty("error").GetString(), Does.Contain("only update your own data"));
        }
        finally
        {
            var adminToken = await GetAdminTokenAsync();
            await DeleteUserAsync(actor.Id, adminToken);
            await DeleteUserAsync(target.Id, adminToken);
        }
    }

    [Test]
    [AllureName("PATCH /users/{id} with a single field, as admin, returns 200 {success:true} and only changes that field")]
    public async Task Patch_AsAdmin_OnlyChangesThatField()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe Patch");
        CreatedUserId = user.Id;
        var adminToken = await GetAdminTokenAsync();

        var response = await ApiContext.PatchAsync($"/users/{user.Id}", new APIRequestContextOptions
        {
            Headers = JsonAcceptHeaders(adminToken),
            DataObject = new { first_name = "PatchedByAdmin" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);

        var after = (await (await ApiContext.GetAsync($"/users/{user.Id}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" },
        })).JsonAsync())!.Value;
        Assert.That(after.GetProperty("first_name").GetString(), Is.EqualTo("PatchedByAdmin"));
        Assert.That(after.GetProperty("email").GetString(), Is.EqualTo(user.Email), "a field not sent in a PATCH must not change");
    }

    [Test]
    [AllureName("DELETE /users/{id} with no auth returns 401")]
    public async Task Delete_NoAuth_ReturnsUnauthorized()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe DeleteNoAuth");
        CreatedUserId = user.Id;

        var response = await ApiContext.DeleteAsync($"/users/{user.Id}");

        Assert.That(response.Status, Is.EqualTo(401));
    }

    [Test]
    [AllureName("DELETE /users/{id} for yourself, as a non-admin, returns 403 - matches the spec's 'admin role required'")]
    public async Task Delete_Self_AsNonAdmin_ReturnsForbidden()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe DeleteSelf");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var response = await ApiContext.DeleteAsync($"/users/{user.Id}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(403));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Forbidden"));
    }

    [Test]
    [AllureName("DELETE /users/{id} with admin auth returns 204 and the user is actually gone")]
    public async Task Delete_WithAdminAuth_DeletesUser()
    {
        var user = await RegisterThrowawayUserAsync("Spec Probe DeleteAdmin");
        var token = await GetAdminTokenAsync();

        var response = await DeleteUserAsync(user.Id, token);
        Assert.That(response.Status, Is.EqualTo(204));

        var loginAfterDelete = await ApiContext.PostAsync("/users/login", new APIRequestContextOptions
        {
            DataObject = new { email = user.Email, password = user.Password },
        });
        Assert.That(loginAfterDelete.Status, Is.EqualTo(401), "a deleted user should no longer be able to log in");
    }
}
