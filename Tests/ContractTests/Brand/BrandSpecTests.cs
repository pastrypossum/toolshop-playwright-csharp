using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Brand;

/// <summary>
/// Contract tests for every <c>/brands*</c> operation against its documented OpenAPI schema
/// (<c>{API_URL}/docs</c>) - the shape, not the business data. One file per resource, grouped
/// by operation below (list / get-by-id / search / create / update / delete), per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Spec-track convention.
///
/// Known gaps tracked separately and deliberately red: see <see cref="BrandAuthGapSpecTests"/>.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Brand")]
[AllureSubSuite("BrandSpecTests")]
public class BrandSpecTests : BrandContractTestBase
{
    private static readonly string[] BrandResponseProperties = ["id", "name", "slug"];

    // ---- GET /brands ----

    [Test]
    [AllureName("GET /brands returns 200 with a JSON array")]
    public async Task Get_ReturnsOkWithJsonArray()
    {
        var response = await ApiContext.GetAsync("/brands");

        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["content-type"], Does.Contain("application/json"));

        var body = await response.JsonAsync();
        Assert.That(body!.Value.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    [AllureName("Every brand matches the BrandResponse schema exactly")]
    public async Task Get_EveryBrandMatchesSchema()
    {
        var response = await ApiContext.GetAsync("/brands");
        var brands = await response.JsonAsync();

        Assert.That(brands!.Value.GetArrayLength(), Is.GreaterThan(0), "expected at least one seeded brand");

        foreach (var brand in brands.Value.EnumerateArray())
        {
            // BrandResponse defines exactly id/name/slug - flag schema drift in either direction.
            Assert.That(brand.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(BrandResponseProperties));

            foreach (var propertyName in BrandResponseProperties)
            {
                var property = brand.GetProperty(propertyName);
                Assert.That(property.ValueKind, Is.EqualTo(JsonValueKind.String), $"'{propertyName}' should be a string");
                Assert.That(property.GetString(), Is.Not.Null.And.Not.Empty, $"'{propertyName}' should not be empty");
            }
        }
    }

    [Test]
    [AllureName("An unsupported method on /brands returns 405 per the documented contract")]
    public async Task Get_UnsupportedMethod_ReturnsMethodNotAllowed()
    {
        // The collection route only documents GET and POST; PUT/PATCH/DELETE belong to
        // /brands/{brandId} and must 405 here per the OpenAPI spec's MethodNotAllowedResponse.
        var response = await ApiContext.PutAsync("/brands");

        Assert.That(response.Status, Is.EqualTo(405));
    }

    // ---- GET /brands/{id} ----

    [Test]
    [AllureName("GET /brands/{id} returns 200 with a BrandResponse for an existing brand")]
    public async Task GetById_ExistingBrand_ReturnsOkWithBrandResponseShape()
    {
        CreatedBrandId = await CreateThrowawayBrandAsync("Spec Probe Brand");

        var response = await ApiContext.GetAsync($"/brands/{CreatedBrandId}");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(BrandResponseProperties));
        Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(CreatedBrandId));
    }

    [Test]
    [AllureName("GET /brands/{id} returns 404 with ItemNotFoundResponse for a missing brand")]
    public async Task GetById_MissingBrand_ReturnsNotFoundWithMessage()
    {
        var response = await ApiContext.GetAsync("/brands/does-not-exist");

        Assert.That(response.Status, Is.EqualTo(404));
        var body = (await response.JsonAsync())!.Value;
        // ItemNotFoundResponse per the spec - a single top-level `message`, not a field-keyed MessageBag.
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Requested item not found"));
    }

    // ---- GET /brands/search, QUERY /brands/search ----

    [Test]
    [AllureName("GET /brands/search returns matching brands as a plain array")]
    public async Task Search_MatchingQuery_ReturnsArrayOfBrands()
    {
        var response = await ApiContext.GetAsync("/brands/search?q=Forge");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0));
        foreach (var brand in body.EnumerateArray())
        {
            Assert.That(brand.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(BrandResponseProperties));
        }
    }

    [Test]
    [AllureName("GET /brands/search with no matches returns an empty array, not 404")]
    public async Task Search_NoMatches_ReturnsEmptyArray()
    {
        var response = await ApiContext.GetAsync("/brands/search?q=zzz-no-such-brand-zzz");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    [AllureName("QUERY /brands/search (RFC 9110 QUERY) matches the GET variant's response shape")]
    public async Task Search_QueryMethodVariant_MatchesGetVariant()
    {
        var response = await ApiContext.FetchAsync("/brands/search", new APIRequestContextOptions
        {
            Method = "QUERY",
            DataObject = new { q = "Forge" },
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0));
    }

    // ---- POST /brands ----

    [Test]
    [AllureName("POST /brands with no auth still succeeds with 201 - spec/implementation gap, asserted on purpose")]
    public async Task Create_ValidBrand_NoAuth_ReturnsCreated()
    {
        var response = await ApiContext.PostAsync("/brands", new APIRequestContextOptions
        {
            DataObject = new { name = "Spec Probe Brand", slug = $"spec-probe-{Guid.NewGuid():N}" },
        });

        Assert.That(response.Status, Is.EqualTo(201));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(BrandResponseProperties));
        CreatedBrandId = body.GetProperty("id").GetString();
    }

    [Test]
    [AllureName("POST /brands with a missing required field returns 422 with a field-keyed message")]
    public async Task Create_MissingRequiredFields_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.PostAsync("/brands", new APIRequestContextOptions
        {
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        // The spec's UnprocessableEntityResponse component has no schema at all - this is the
        // real shape, found by probing the live endpoint, not documented anywhere in /docs.
        Assert.That(body.GetProperty("name")[0].GetString(), Is.EqualTo("The name field is required."));
        Assert.That(body.GetProperty("slug")[0].GetString(), Is.EqualTo("The slug field is required."));
    }

    [Test]
    [AllureName("POST /brands with a slug that already exists returns 409 with a field-keyed message")]
    public async Task Create_DuplicateSlug_ReturnsConflict()
    {
        var response = await ApiContext.PostAsync("/brands", new APIRequestContextOptions
        {
            DataObject = new { name = "Duplicate Probe", slug = "forgeflex-tools" }, // seeded brand's real slug
        });

        Assert.That(response.Status, Is.EqualTo(409));
        var body = (await response.JsonAsync())!.Value;
        // DuplicateConflictResponse's first oneOf branch - field-level MessageBag.
        Assert.That(body.GetProperty("slug")[0].GetString(), Does.Contain("already exists"));
    }

    // ---- PUT /brands/{id}, PATCH /brands/{id} ----

    [Test]
    [AllureName("PUT /brands/{id} with no auth still returns 200 {success:true} - spec/implementation gap, asserted on purpose")]
    public async Task Put_ValidBrand_NoAuth_ReturnsSuccess()
    {
        CreatedBrandId = await CreateThrowawayBrandAsync("Update Probe Brand");

        var response = await ApiContext.PutAsync($"/brands/{CreatedBrandId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Updated Name", slug = $"update-probe-{Guid.NewGuid():N}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);
    }

    [Test]
    [AllureName("PATCH /brands/{id} with a single field returns 200 {success:true}, and only touches that field")]
    public async Task Patch_SingleField_OnlyChangesThatField()
    {
        CreatedBrandId = await CreateThrowawayBrandAsync("Update Probe Brand");
        var before = await ApiContext.GetAsync($"/brands/{CreatedBrandId}");
        var slugBeforePatch = (await before.JsonAsync())!.Value.GetProperty("slug").GetString();

        var response = await ApiContext.PatchAsync($"/brands/{CreatedBrandId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Patched Name" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);

        var afterBody = (await (await ApiContext.GetAsync($"/brands/{CreatedBrandId}")).JsonAsync())!.Value;
        Assert.That(afterBody.GetProperty("name").GetString(), Is.EqualTo("Patched Name"), "the field that was patched should change");
        // The actual distinction between PATCH and PUT: a field not sent must be left alone.
        Assert.That(afterBody.GetProperty("slug").GetString(), Is.EqualTo(slugBeforePatch), "a field not sent in a PATCH must not change");
    }

    [Test]
    [AllureName("PUT /brands/{id} for a missing brand returns 404 with a message")]
    public async Task Put_MissingBrand_ReturnsNotFound()
    {
        var response = await ApiContext.PutAsync("/brands/does-not-exist", new APIRequestContextOptions
        {
            DataObject = new { name = "Doesn't matter", slug = "doesnt-matter" },
        });

        Assert.That(response.Status, Is.EqualTo(404));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Requested item not found"));
    }

    // ---- DELETE /brands/{id} ----

    [Test]
    [AllureName("DELETE /brands/{id} with no auth returns 401 Unauthorized")]
    public async Task Delete_NoAuth_ReturnsUnauthorized()
    {
        var brandId = await CreateThrowawayBrandAsync("Delete Probe Brand");

        var response = await ApiContext.DeleteAsync($"/brands/{brandId}");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));

        // Clean up properly now that we've proven the unauthenticated attempt didn't delete it.
        var token = await GetAdminTokenAsync();
        await DeleteBrandAsync(brandId, token);
    }

    [Test]
    [AllureName("DELETE /brands/{id} with admin auth returns 204 and the brand is actually gone")]
    public async Task Delete_WithAuth_DeletesBrand()
    {
        var brandId = await CreateThrowawayBrandAsync("Delete Probe Brand");
        var token = await GetAdminTokenAsync();

        var response = await DeleteBrandAsync(brandId, token);
        Assert.That(response.Status, Is.EqualTo(204));

        var reloaded = await ApiContext.GetAsync($"/brands/{brandId}");
        Assert.That(reloaded.Status, Is.EqualTo(404));
    }

    [Test]
    [AllureName("DELETE /brands/{id} for a missing brand returns 404, even with valid auth")]
    public async Task Delete_MissingBrand_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();

        var response = await DeleteBrandAsync("does-not-exist", token);

        Assert.That(response.Status, Is.EqualTo(404));
    }

    [Test]
    [AllureName("DELETE /brands/{id} for a brand used by a product returns 409, not the spec's undocumented generic shape")]
    public async Task Delete_BrandInUse_ReturnsConflict()
    {
        var token = await GetAdminTokenAsync();
        var brandId = await CreateThrowawayBrandAsync("Delete Conflict Probe", token);

        var categoryId = (await (await ApiContext.GetAsync("/categories")).JsonAsync())!.Value[0].GetProperty("id").GetString();
        var sampleProduct = (await (await ApiContext.GetAsync("/products")).JsonAsync())!.Value.GetProperty("data")[0];
        var imageId = sampleProduct.GetProperty("product_image").GetProperty("id").GetString();

        var productResponse = await ApiContext.PostAsync("/products", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new
            {
                name = "Delete Conflict Probe Product",
                description = "created to make the brand it references in-use",
                price = 9.99,
                category_id = categoryId,
                brand_id = brandId,
                product_image_id = imageId,
                is_location_offer = false,
                is_rental = false,
            },
        });
        Assert.That(productResponse.Status, Is.EqualTo(201), "setup failed: could not create the dependent product");
        var productId = (await productResponse.JsonAsync())!.Value.GetProperty("id").GetString();

        try
        {
            var response = await DeleteBrandAsync(brandId, token);

            Assert.That(response.Status, Is.EqualTo(409));
            var body = (await response.JsonAsync())!.Value;
            // The spec's ConflictResponse component has no schema at all - this real shape
            // ({"success":false,"message":"..."}) was found by probing the live endpoint, not
            // documented anywhere in /docs, and differs from every other error shape in this
            // API (no top-level `success` field elsewhere).
            Assert.That(body.GetProperty("success").GetBoolean(), Is.False);
            Assert.That(body.GetProperty("message").GetString(), Does.Contain("used elsewhere"));
        }
        finally
        {
            await ApiContext.DeleteAsync($"/products/{productId}", new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            });
            await DeleteBrandAsync(brandId, token);
        }
    }
}
