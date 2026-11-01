using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Postcode;

/// <summary>
/// Contract tests for the single <c>GET /postcode-lookup</c> operation against its documented
/// OpenAPI schema (<c>{API_URL}/docs</c>) - the shape, not the business data. No auth, no
/// created/deleted resources - the endpoint is public and stateless, so unlike
/// <c>BrandContractTestBase</c>/<c>CategoryContractTestBase</c> there is no shared setup/teardown
/// worth extracting; this extends <see cref="ApiTest"/> directly, per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s "don't write bespoke setup unless the shared
/// helpers genuinely can't produce what the test needs" - there's simply nothing to set up.
///
/// Probing the live endpoint (per the skill's "check the running endpoint, and note where it
/// disagrees with the docs") surfaced one real gap from the documented contract: a request that
/// fails validation only gets a JSON <c>{message, errors}</c> body when it sends
/// <c>Accept: application/json</c> - otherwise Laravel's default (non-FormRequest) validation
/// failure path redirects (302) to the SPA instead. Every other validated endpoint in this API
/// (e.g. <c>POST /brands</c>) goes through a <c>BaseFormRequest</c> subclass that forces a JSON
/// response regardless of <c>Accept</c> - <c>PostcodeController::lookup</c> uses the framework's
/// plain <c>$request-&gt;validate()</c> instead, so it didn't inherit that behavior. See
/// <see cref="Get_MissingRequiredFields_WithoutJsonAccept_Redirects"/>, which documents this as
/// real, current behavior (not a "should be" gap test) since the real consumer (Angular's
/// <c>HttpClient</c>, see <c>PostcodePactTests</c>) always sends <c>Accept: application/json,
/// text/plain, */*</c> and never hits this path in practice.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Postcode")]
[AllureSubSuite("PostcodeSpecTests")]
public class PostcodeSpecTests : ApiTest
{
    private static readonly string[] LookupResponseProperties =
        ["street", "house_number", "city", "state", "country", "postcode"];

    private static readonly Dictionary<string, string> JsonAcceptHeader =
        new() { ["Accept"] = "application/json" };

    // ---- GET /postcode-lookup - happy path ----

    [Test]
    [AllureName("GET /postcode-lookup with all params returns 200 with the full LookupResponse schema")]
    public async Task Get_ValidRequest_ReturnsOkWithFullSchema()
    {
        var response = await ApiContext.GetAsync("/postcode-lookup?country=NL&postcode=1011AB&house_number=5");

        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["content-type"], Does.Contain("application/json"));

        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(LookupResponseProperties));
        foreach (var propertyName in LookupResponseProperties)
        {
            Assert.That(body.GetProperty(propertyName).ValueKind, Is.EqualTo(JsonValueKind.String),
                $"'{propertyName}' should be a string");
        }
    }

    [Test]
    [AllureName("GET /postcode-lookup without house_number still returns 200 with a generated one")]
    public async Task Get_HouseNumberOmitted_GeneratesHouseNumber()
    {
        // house_number is documented 'nullable' - omitting it must not 422, and the driver fills
        // in a value rather than echoing back an empty string.
        var response = await ApiContext.GetAsync("/postcode-lookup?country=NL&postcode=1011AB");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("house_number").GetString(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [AllureName("GET /postcode-lookup with the same country+postcode is idempotent across repeated calls")]
    public async Task Get_SameQuery_ReturnsIdempotentResponse()
    {
        // GET is documented as a safe, idempotent operation - two identical requests must return
        // the same locality, not a new random one each time.
        var first = (await (await ApiContext.GetAsync("/postcode-lookup?country=NL&postcode=1011AB&house_number=5")).JsonAsync())!.Value;
        var second = (await (await ApiContext.GetAsync("/postcode-lookup?country=NL&postcode=1011AB&house_number=5")).JsonAsync())!.Value;

        Assert.That(second.GetProperty("street").GetString(), Is.EqualTo(first.GetProperty("street").GetString()));
        Assert.That(second.GetProperty("city").GetString(), Is.EqualTo(first.GetProperty("city").GetString()));
        Assert.That(second.GetProperty("state").GetString(), Is.EqualTo(first.GetProperty("state").GetString()));
    }

    [Test]
    [AllureName("GET /postcode-lookup: city/state depend only on country+postcode, not house_number")]
    public async Task Get_DifferentHouseNumber_SameCityAndState()
    {
        var a = (await (await ApiContext.GetAsync("/postcode-lookup?country=NL&postcode=1011AB&house_number=5")).JsonAsync())!.Value;
        var b = (await (await ApiContext.GetAsync("/postcode-lookup?country=NL&postcode=1011AB&house_number=99")).JsonAsync())!.Value;

        Assert.That(b.GetProperty("city").GetString(), Is.EqualTo(a.GetProperty("city").GetString()));
        Assert.That(b.GetProperty("state").GetString(), Is.EqualTo(a.GetProperty("state").GetString()));
        Assert.That(b.GetProperty("house_number").GetString(), Is.EqualTo("99"));
    }

    [Test]
    [AllureName("GET /postcode-lookup normalizes country to uppercase in the response")]
    public async Task Get_LowercaseCountry_NormalizedToUppercaseInResponse()
    {
        var response = await ApiContext.GetAsync("/postcode-lookup?country=nl&postcode=1011AB&house_number=5");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("country").GetString(), Is.EqualTo("NL"));
    }

    [Test]
    [AllureName("GET /postcode-lookup normalizes postcode to uppercase in the response")]
    public async Task Get_LowercasePostcode_NormalizedToUppercaseInResponse()
    {
        var response = await ApiContext.GetAsync("/postcode-lookup?country=NL&postcode=1011ab&house_number=5");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("postcode").GetString(), Is.EqualTo("1011AB"));
    }

    // ---- GET /postcode-lookup - country/postcode format cross-check ----

    [Test]
    [AllureName("GET /postcode-lookup rejects a postcode whose shape does not fit the selected country")]
    public async Task Get_PostcodeFormatMismatchForCountry_ReturnsUnprocessableEntityWithMessage()
    {
        // Dutch-shaped postcode while Austria (4-digit-only) is selected.
        var response = await ApiContext.GetAsync("/postcode-lookup?country=AT&postcode=1011AB&house_number=5");

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        // This 422 comes from the controller's manual PostcodeFormat check, not the framework's
        // validate() - a single top-level `message`, no `errors` MessageBag (contrast with
        // Get_MissingRequiredFields_WithJsonAccept_ReturnsFieldKeyedErrors below).
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "message" }));
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("The postal code format is not valid for the selected country."));
    }

    [TestCase("NL", "1011AB")]
    [TestCase("DE", "10115")]
    [TestCase("GB", "SW1A 1AA")]
    [TestCase("US", "90210")]
    [TestCase("CA", "K1A 0B1")]
    [AllureName("GET /postcode-lookup accepts a postcode whose shape matches the selected country")]
    public async Task Get_PostcodeFormatMatchesCountry_ReturnsOk(string country, string postcode)
    {
        var response = await ApiContext.GetAsync($"/postcode-lookup?country={country}&postcode={Uri.EscapeDataString(postcode)}&house_number=1");

        Assert.That(response.Status, Is.EqualTo(200));
    }

    [Test]
    [AllureName("GET /postcode-lookup accepts any postcode shape for a country with no known format")]
    public async Task Get_UnknownCountry_AcceptsAnyPostcodeShape()
    {
        // "ZZ" isn't in PostcodeFormat's country list, so the shape check is unconstrained -
        // a non-digit postcode that would fail every known-country pattern must still succeed.
        var response = await ApiContext.GetAsync("/postcode-lookup?country=ZZ&postcode=ABC-123&house_number=5");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("country").GetString(), Is.EqualTo("ZZ"));
    }

    // ---- GET /postcode-lookup - required-field validation ----

    [Test]
    [AllureName("GET /postcode-lookup with no params and Accept:application/json returns 422 with a field-keyed errors object")]
    public async Task Get_MissingRequiredFields_WithJsonAccept_ReturnsFieldKeyedErrors()
    {
        var response = await ApiContext.GetAsync("/postcode-lookup", new APIRequestContextOptions { Headers = JsonAcceptHeader });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        // Framework-default ValidationException shape ({message, errors: {field: [...]}}) -
        // different from the hand-rolled {message} shape the format check above returns.
        Assert.That(body.GetProperty("message").ValueKind, Is.EqualTo(JsonValueKind.String));
        var errors = body.GetProperty("errors");
        Assert.That(errors.GetProperty("country")[0].GetString(), Is.EqualTo("The country field is required."));
        Assert.That(errors.GetProperty("postcode")[0].GetString(), Is.EqualTo("The postcode field is required."));
    }

    [Test]
    [AllureName("GET /postcode-lookup missing only postcode returns a field-keyed error for postcode alone")]
    public async Task Get_MissingOnlyPostcode_ReturnsErrorForPostcodeOnly()
    {
        var response = await ApiContext.GetAsync("/postcode-lookup?country=NL", new APIRequestContextOptions { Headers = JsonAcceptHeader });

        Assert.That(response.Status, Is.EqualTo(422));
        var errors = (await response.JsonAsync())!.Value.GetProperty("errors");
        Assert.That(errors.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "postcode" }));
    }

    [Test]
    [AllureName("GET /postcode-lookup with country over 40 chars returns 422 with a max-length message")]
    public async Task Get_CountryExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.GetAsync(
            $"/postcode-lookup?country={new string('A', 41)}&postcode=1011AB",
            new APIRequestContextOptions { Headers = JsonAcceptHeader });

        Assert.That(response.Status, Is.EqualTo(422));
        var errors = (await response.JsonAsync())!.Value.GetProperty("errors");
        Assert.That(errors.GetProperty("country")[0].GetString(), Does.Contain("must not be greater than 40 characters"));
    }

    [Test]
    [AllureName("GET /postcode-lookup with postcode over 10 chars returns 422 with a max-length message")]
    public async Task Get_PostcodeExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.GetAsync(
            "/postcode-lookup?country=ZZ&postcode=ABCDE12345X",
            new APIRequestContextOptions { Headers = JsonAcceptHeader });

        Assert.That(response.Status, Is.EqualTo(422));
        var errors = (await response.JsonAsync())!.Value.GetProperty("errors");
        Assert.That(errors.GetProperty("postcode")[0].GetString(), Does.Contain("must not be greater than 10 characters"));
    }

    [Test]
    [AllureName("GET /postcode-lookup with no params and no Accept:application/json redirects instead of returning JSON")]
    public async Task Get_MissingRequiredFields_WithoutJsonAccept_Redirects()
    {
        // Documents real, current behavior (not an aspirational gap test) - see this file's
        // class-level remarks. APIRequestContext sends no explicit Accept header by default,
        // same as the bare curl probe this was verified against.
        var response = await ApiContext.GetAsync("/postcode-lookup", new APIRequestContextOptions
        {
            MaxRedirects = 0,
        });

        Assert.That(response.Status, Is.EqualTo(302));
    }

    // ---- Unsupported method ----

    [Test]
    [AllureName("An unsupported method on /postcode-lookup returns 405 with a message")]
    public async Task Put_UnsupportedMethod_ReturnsMethodNotAllowed()
    {
        var response = await ApiContext.PutAsync("/postcode-lookup");

        Assert.That(response.Status, Is.EqualTo(405));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Method is not allowed for the requested route"));
    }
}
