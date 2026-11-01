using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Brand;

/// <summary>
/// Contract tests built from real, recorded consumer interactions rather than the OpenAPI spec
/// alone - the layer on top of <see cref="BrandSpecTests"/>'s specification-based checks. One
/// file per resource, covering every consumer dependency found for it, per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Pact-track convention.
///
/// <see cref="GetBrands_MatchesRecordedConsumerExample"/> and
/// <see cref="BrandId_FromBrandsResponse_FiltersProductsCorrectly"/>: the fixture in
/// <c>Data/Contract/Brand/BrandsConsumerExample.json</c> was captured with <c>playwright-cli</c>
/// against the real Angular app (http://localhost:4200, the overview page's
/// <c>BrandService.getBrands()</c> call), not hand-written. These exercise the one thing the
/// <c>/brands</c> spec alone cannot express: the real consumer takes a brand <c>id</c> from this
/// response and later sends it straight back as a <c>GET /products?by_brand={id}</c> filter
/// (see <c>category.component.ts</c> -&gt; <c>product.service.ts#getProductsByCategoryAndBrand</c>).
///
/// <see cref="Create_ValidationFailure_ReturnsFieldKeyedErrors"/> and
/// <see cref="Create_SlugConflict_ReturnsFieldKeyedErrors"/>: built from tracing a real
/// consumer's error-handling code - `admin/brands-add-edit/brands-add-edit.component.ts`'s
/// `createBrand()`/`updateBrand()` error callbacks do
/// <c>Object.values(err).map(v => Array.isArray(v) ? v.join(...) : v)</c>, which only renders
/// something useful when the error body is a field-keyed MessageBag
/// (<c>{"name": ["..."], "slug": ["..."]}</c>), not a single <c>{"message": "..."}</c> string.
/// These assert the provider still returns that shape for create validation and conflict
/// failures.
///
/// Known gap, found but not covered here: `BrandService.handleError` (see `brand.service.ts`)
/// collapses every HTTP error into a generic single-string `Error` *before* the component's own
/// handler ever runs, so in the real app that field-keyed body never actually reaches
/// `Object.values(err)` - the UI likely shows a blank error message on validation failure.
/// That's a real bug, but it's a consumer-side one, not a provider-contract violation - out of
/// scope per this skill's own "Out of scope" section.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Brand")]
[AllureSubSuite("BrandPactTests")]
public class BrandPactTests : ApiTest
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "Data", "Contract", "Brand", "BrandsConsumerExample.json");

    [Test]
    [AllureName("GET /brands still matches the response shape a real consumer received")]
    public async Task GetBrands_MatchesRecordedConsumerExample()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath));
        var recordedResponse = fixture.RootElement.GetProperty("response");

        var response = await ApiContext.GetAsync("/brands", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Accept"] = "application/json, text/plain, */*" },
        });

        Assert.That(response.Status, Is.EqualTo(recordedResponse.GetProperty("status").GetInt32()));
        Assert.That(response.Headers["content-type"], Does.Contain(recordedResponse.GetProperty("contentType").GetString()!));

        var liveBrandNames = (await response.JsonAsync())!.Value.EnumerateArray()
            .Select(b => b.GetProperty("name").GetString())
            .ToList();
        var recordedBrandNames = recordedResponse.GetProperty("body").EnumerateArray()
            .Select(b => b.GetProperty("name").GetString())
            .ToList();

        // Tied to this demo app's fixed seed data - if the seeded brands genuinely change,
        // re-record the fixture with playwright-cli rather than loosening this assertion.
        Assert.That(liveBrandNames, Is.SupersetOf(recordedBrandNames));
    }

    [Test]
    [AllureName("A brand id from GET /brands filters GET /products as the real consumer relies on")]
    public async Task BrandId_FromBrandsResponse_FiltersProductsCorrectly()
    {
        var brandsResponse = await ApiContext.GetAsync("/brands");
        var brands = (await brandsResponse.JsonAsync())!.Value;

        // Scoped to the two permanent seeded brands, not every brand currently in the
        // collection: other tests in this suite create/delete throwaway brands as part of
        // their own setup/teardown, and under parallel execution this test can otherwise
        // observe one mid-lifecycle (no products yet, or already deleted) - a real race, not
        // flakiness in this test's own logic. The seeded brands are never created or deleted
        // by anything in this suite, so they're safe to rely on regardless of what else is
        // running concurrently.
        var recordedBrandNames = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath))
            .RootElement.GetProperty("response").GetProperty("body").EnumerateArray()
            .Select(b => b.GetProperty("name").GetString())
            .ToHashSet();

        foreach (var brand in brands.EnumerateArray().Where(b => recordedBrandNames.Contains(b.GetProperty("name").GetString())))
        {
            var brandId = brand.GetProperty("id").GetString();
            var brandName = brand.GetProperty("name").GetString();

            var productsResponse = await ApiContext.GetAsync($"/products?by_brand={brandId}");
            Assert.That(productsResponse.Status, Is.EqualTo(200));

            var products = (await productsResponse.JsonAsync())!.Value.GetProperty("data");
            Assert.That(products.GetArrayLength(), Is.GreaterThan(0),
                $"expected at least one product for brand '{brandName}'");

            foreach (var product in products.EnumerateArray())
            {
                Assert.That(product.GetProperty("brand").GetProperty("id").GetString(), Is.EqualTo(brandId),
                    $"product returned for by_brand={brandId} ('{brandName}') belongs to a different brand");
            }
        }
    }

    [Test]
    [AllureName("POST /brands validation failure returns a field-keyed MessageBag, as the admin UI's error renderer expects")]
    public async Task Create_ValidationFailure_ReturnsFieldKeyedErrors()
    {
        var response = await ApiContext.PostAsync("/brands", new APIRequestContextOptions
        {
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;

        // Object.values(err) in the consumer needs every top-level value to be a string[]
        // (or a plain string) - not a nested object, not a bare message. Assert that shape.
        foreach (var property in body.EnumerateObject())
        {
            Assert.That(property.Value.ValueKind, Is.EqualTo(JsonValueKind.Array),
                $"'{property.Name}' must be an array for Object.values(err).join() to render it correctly");
        }
    }

    [Test]
    [AllureName("POST /brands slug conflict returns a field-keyed MessageBag, as the admin UI's error renderer expects")]
    public async Task Create_SlugConflict_ReturnsFieldKeyedErrors()
    {
        var response = await ApiContext.PostAsync("/brands", new APIRequestContextOptions
        {
            DataObject = new { name = "Duplicate Probe", slug = "forgeflex-tools" },
        });

        Assert.That(response.Status, Is.EqualTo(409));
        var body = (await response.JsonAsync())!.Value;

        foreach (var property in body.EnumerateObject())
        {
            Assert.That(property.Value.ValueKind, Is.EqualTo(JsonValueKind.Array),
                $"'{property.Name}' must be an array for Object.values(err).join() to render it correctly");
        }
    }
}
