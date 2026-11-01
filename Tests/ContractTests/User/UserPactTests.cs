using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.User;

/// <summary>
/// Contract tests built from real consumer dependencies rather than the OpenAPI spec alone - the
/// layer on top of <see cref="UserSpecTests"/>'s specification-based checks. One file per
/// resource, covering every consumer dependency found for it, per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Pact-track convention. Dependencies here
/// were traced by reading the Angular frontend source directly (available alongside this repo at
/// <c>practice-software-testing/sprint5/UI/src/app</c>), not recorded with <c>playwright-cli</c>
/// - the request/response shapes asserted below were verified live against the deployed
/// environment.
///
/// <see cref="LoginFailure_MatchesConsumerExpectedErrorShape"/>: this is the one genuinely
/// fragile dependency found in this resource. <c>auth/login/login.component.ts</c>'s
/// <c>handleLoginError(err)</c> does <c>err.error === 'Unauthorized' ? 'Invalid email or
/// password' : ...</c> - a strict equality check against a bare string. The <c>err</c> it
/// receives is not the raw <c>HttpErrorResponse</c> despite its declared type: it's already been
/// unwrapped once by <c>shared/customer-account.service.ts</c>'s <c>errorHandler</c>
/// (<c>throwError(() => error.error || "server error.")</c>), so by the time the component sees
/// it, <c>err</c> *is* the response body and <c>err.error</c> is that body's own <c>error</c>
/// field. For the friendly "Invalid email or password" message to ever appear, the live 401
/// response body has to be exactly <c>{"error": "Unauthorized"}</c> - a JSON *object* whose
/// <c>error</c> property is the literal string "Unauthorized", not a bare string response body
/// and not <c>{"message": "Unauthorized"}</c> (the shape every other 401 in this API uses, see
/// <see cref="UserSpecTests.Me_NoAuth_ReturnsUnauthorized"/>). Change either the envelope or the
/// string and this consumer's friendly error message silently stops showing.
///
/// <see cref="MeId_FlowsIntoSelfProfileUpdate"/>: traced from
/// <c>account/profile/profile.component.ts</c>, which gets the logged-in user's own id from
/// <c>CustomerAccountService.getDetails()</c> (<c>GET /users/me</c>) and later sends it straight
/// back as <c>CustomerAccountService.update(id, user)</c>'s path parameter
/// (<c>PUT /users/{id}</c>) - the self-service profile edit flow depends on <c>/me</c>'s
/// <c>id</c> matching a <c>{userId}</c> the same token is allowed to <c>PUT</c>.
///
/// <see cref="Register_ValidationFailure_ReturnsFieldKeyedErrors"/>: built from tracing
/// <c>auth/register/register.component.ts</c>'s error callback, which reads
/// <c>body?.errors ?? body</c> before doing <c>Object.values(fieldErrors).map(...)</c> - written
/// to accept either the flat <c>{field: [...]}</c> shape or Laravel's wrapped
/// <c>{message, errors}</c> envelope (change-password/forgot-password use the latter, see
/// <see cref="UserSpecTests.ChangePassword_MismatchedConfirmation_ReturnsUnprocessableEntity"/>).
/// This asserts registration's own validation failures still use the flat shape that fallback
/// was written for.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("User")]
[AllureSubSuite("UserPactTests")]
public class UserPactTests : UserContractTestBase
{
    [Test]
    [AllureName("POST /users/login failure still returns exactly {error: 'Unauthorized'}, as the login form's error handling depends on")]
    public async Task LoginFailure_MatchesConsumerExpectedErrorShape()
    {
        var response = await ApiContext.PostAsync("/users/login", new APIRequestContextOptions
        {
            DataObject = new { email = "no-such-consumer-probe@example.com", password = "whatever-wrong" },
        });

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;

        // handleLoginError does `err.error === 'Unauthorized'` on the already-unwrapped body -
        // an exact string match, not Does.Contain. Any other shape (wrapping, renaming the key,
        // changing the string) makes the consumer fall through to a less helpful message.
        Assert.That(body.GetProperty("error").GetString(), Is.EqualTo("Unauthorized"));
    }

    [Test]
    [AllureName("The id from GET /users/me is accepted by PUT /users/{id} for that same account, as the profile self-edit form relies on")]
    public async Task MeId_FlowsIntoSelfProfileUpdate()
    {
        var user = await RegisterThrowawayUserAsync("Pact Probe Profile");
        CreatedUserId = user.Id;
        var token = await LoginAsAsync(user.Email, user.Password);

        var meResponse = await ApiContext.GetAsync("/users/me", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });
        var selfId = (await meResponse.JsonAsync())!.Value.GetProperty("id").GetString();
        Assert.That(selfId, Is.EqualTo(user.Id), "GET /users/me should identify the same account that just logged in");

        var updateResponse = await ApiContext.PutAsync($"/users/{selfId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}", ["Accept"] = "application/json" },
            DataObject = new
            {
                first_name = "ProfileEdited",
                last_name = "Probe",
                email = user.Email,
                address = new { street = "Street 1", city = "City", country = "Country" },
            },
        });

        Assert.That(updateResponse.Status, Is.EqualTo(200),
            "the id a consumer reads from /users/me must be a valid PUT /users/{id} target for the same token");
    }

    [Test]
    [AllureName("POST /users/register validation failure returns a flat field-keyed MessageBag, as the registration form's error renderer expects")]
    public async Task Register_ValidationFailure_ReturnsFieldKeyedErrors()
    {
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;

        // register.component.ts falls back to `body` directly (no `.errors` wrapper) when the
        // body has no `errors` key - this only renders correctly if every top-level value here
        // is itself a string[] (or a plain string), per its Object.values(fieldErrors).map(...).
        foreach (var property in body.EnumerateObject())
        {
            Assert.That(property.Value.ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Array),
                $"'{property.Name}' must be an array for the flat-shape Object.values(...).join() fallback to render it correctly");
        }
    }
}
