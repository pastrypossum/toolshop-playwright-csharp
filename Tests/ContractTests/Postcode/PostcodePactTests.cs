using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Postcode;

/// <summary>
/// Contract tests built from tracing the real consumer rather than the OpenAPI spec alone - the
/// layer on top of <see cref="PostcodeSpecTests"/>'s specification-based checks, per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Pact-track convention. The frontend source
/// is available here, so per the skill's guidance this was done by reading
/// <c>_services/postcode.service.ts</c> and its two callers directly rather than capturing with
/// <c>playwright-cli</c> (no running dev server/browser session was available for this pass -
/// <c>Data/Contract/Postcode/PostcodeLookupConsumerExample.json</c>'s fixture says so plainly
/// rather than overclaiming a recorded capture).
///
/// Consumer: <c>checkout/address/address.component.ts#tryPostcodeLookup()</c> and
/// <c>auth/register/register.component.ts#tryPostcodeLookup()</c> - both built on the same
/// <c>PostcodeService.lookup()</c> and both call it identically.
///
/// <see cref="GetLookup_MatchesRecordedConsumerExample"/>,
/// <see cref="SuccessResponse_StreetCityState_ArePresentAndStringTyped"/>: the real consumer's
/// success handler does <c>addressGroup.patchValue({ street: result.street, city: result.city,
/// state: result.state })</c> - exactly three of the six documented response fields, with no
/// null/undefined guard on any of them. A missing or null <c>street</c>/<c>city</c>/<c>state</c>
/// would throw inside Angular's reactive-forms <c>patchValue</c>, not fail gracefully, so this is
/// the one thing about the response shape the spec track's "is it a string" check doesn't by
/// itself guarantee for every real input (e.g. a locale with no state data, see
/// <c>FakerPostcodeDriver::state()</c>) - it still has to be <c>""</c>, never absent or null.
///
/// <see cref="FormatMismatchError_HasDisplayableMessage"/>: the consumer's error handler reads
/// <c>err?.error?.message</c> to populate <c>postcodeLookupError</c>. Both callers guard the
/// lookup behind <c>if (!country || !postcode || !houseNumber) return;</c>, so the
/// framework-validation 422 ({message, errors}, see
/// <see cref="PostcodeSpecTests.Get_MissingRequiredFields_WithJsonAccept_ReturnsFieldKeyedErrors"/>)
/// can never actually be triggered by either real caller - the only error status a user can
/// cause in practice is the country/postcode format-mismatch 422. That's the one asserted here.
///
/// Found but out of scope (per the skill's "business logic ... after receiving a response" /
/// internal-reimplementation exclusion): <c>register.component.ts</c>'s own
/// <c>postalCodeValidator()</c> re-implements a per-country postcode shape check client-side
/// (<c>postcodePatterns</c>) that blocks calling the lookup at all when it fails - a second,
/// independently-maintained copy of the same rule the API's <c>PostcodeFormat</c> class enforces
/// server-side. Whether the two pattern tables agree never crosses the wire (the client simply
/// never sends a request its own validator rejects), so there's nothing here for a contract test
/// to observe - flagged, not tested.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Postcode")]
[AllureSubSuite("PostcodePactTests")]
public class PostcodePactTests : ApiTest
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Data", "Contract", "Postcode", "PostcodeLookupConsumerExample.json");

    [Test]
    [AllureName("GET /postcode-lookup still matches the response a real consumer call would receive")]
    public async Task GetLookup_MatchesRecordedConsumerExample()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath));
        var recordedRequest = fixture.RootElement.GetProperty("request");
        var recordedResponse = fixture.RootElement.GetProperty("response");
        var query = recordedRequest.GetProperty("query");

        var response = await ApiContext.GetAsync(
            $"/postcode-lookup?country={query.GetProperty("country").GetString()}" +
            $"&postcode={query.GetProperty("postcode").GetString()}" +
            $"&house_number={query.GetProperty("house_number").GetString()}",
            new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Accept"] = "application/json, text/plain, */*" },
            });

        Assert.That(response.Status, Is.EqualTo(recordedResponse.GetProperty("status").GetInt32()));
        Assert.That(response.Headers["content-type"], Does.Contain(recordedResponse.GetProperty("contentType").GetString()!));

        // Deterministic driver, fixed seed inputs - the real consumer gets exactly this body
        // back every time, not just a same-shaped one.
        var liveBody = (await response.JsonAsync())!.Value;
        var recordedBody = recordedResponse.GetProperty("body");
        foreach (var property in recordedBody.EnumerateObject())
        {
            Assert.That(liveBody.GetProperty(property.Name).GetString(), Is.EqualTo(property.Value.GetString()),
                $"'{property.Name}' drifted from the recorded consumer example");
        }
    }

    [TestCase("NL", "1011AB", "5")] // locale with state data
    [TestCase("GB", "SW1A 1AA", "10")] // locale where Faker has no state list (empty string, not null)
    [AllureName("The three fields address.component.ts patches into the form (street/city/state) are always present strings")]
    public async Task SuccessResponse_StreetCityState_ArePresentAndStringTyped(string country, string postcode, string houseNumber)
    {
        var response = await ApiContext.GetAsync($"/postcode-lookup?country={country}&postcode={Uri.EscapeDataString(postcode)}&house_number={houseNumber}");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;

        // addressGroup.patchValue({ street, city, state }) would throw on a missing/null value -
        // it must be a string, even if that string is empty for a state-less locale like GB.
        foreach (var field in new[] { "street", "city", "state" })
        {
            Assert.That(body.TryGetProperty(field, out var value), Is.True, $"'{field}' missing from the response");
            Assert.That(value.ValueKind, Is.EqualTo(JsonValueKind.String), $"'{field}' must be a string for patchValue()");
        }
    }

    [Test]
    [AllureName("The only error the UI's lookup guard can actually trigger (format mismatch) has a displayable message")]
    public async Task FormatMismatchError_HasDisplayableMessage()
    {
        // Dutch-shaped postcode under Austria - the one error both components' guard
        // (`if (!country || !postcode || !houseNumber) return;`) still lets through, since all
        // three fields are non-empty.
        var response = await ApiContext.GetAsync("/postcode-lookup?country=AT&postcode=1011AB&house_number=5");

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;

        // err?.error?.message must resolve to a real, non-empty string - the component falls
        // back to a generic message only when this is absent/falsy.
        Assert.That(body.TryGetProperty("message", out var message), Is.True);
        Assert.That(message.ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(message.GetString(), Is.Not.Null.And.Not.Empty);
    }
}
