using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.ProductSpec;

/// <summary>
/// Contract tests for every <c>/products/{productId}/specs*</c> operation plus the standalone
/// <c>/product-specs/names</c> aggregate, against the documented OpenAPI schema
/// (<c>{API_URL}/docs</c>) - the shape, not the business data. One file per resource, grouped by
/// operation below (list / create / get-by-id / update / delete / names), per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Spec-track convention.
///
/// Unlike every other write-gated resource in this suite (Brand/Category's <c>destroy</c>,
/// <see cref="Toolshop.Playwright.Tests.Tests.ContractTests.Product.ProductSpecTests.Delete_NoAuth_ReturnsUnauthorized"/>),
/// <c>ProductSpecController</c> gates its writes with plain <c>auth:users</c>, not
/// <c>role:admin</c> - see <see cref="Create_WithCustomerAuth_Succeeds"/>. This is a real,
/// deliberate asymmetry (anyone signed in can maintain spec data), not a gap being tracked here;
/// there's no separate <c>ProductSpecAuthGapSpecTests</c> file for that reason.
///
/// The Product/ProductSpec relationship itself - a spec created under a product id reflected
/// back through <c>GET /products/{id}</c>'s own undocumented <c>specs</c> field, the
/// <c>by_spec</c> cross-filter, cascade delete - is covered in <c>ProductSpecPactTests.cs</c>,
/// not duplicated here.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("ProductSpec")]
[AllureSubSuite("ProductSpecSpecTests")]
public class ProductSpecSpecTests : ProductSpecContractTestBase
{
    private static readonly string[] ProductSpecResponseProperties = ["id", "product_id", "spec_name", "spec_value", "spec_unit"];

    // ---- GET /products/{productId}/specs ----

    [Test]
    [AllureName("GET /products/{productId}/specs returns 200 with an empty array for a product with no specs")]
    public async Task Get_ProductWithNoSpecs_ReturnsEmptyArray()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec List Probe Product", token);

        var response = await ApiContext.GetAsync($"/products/{CreatedProductId}/specs");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    [AllureName("GET /products/{productId}/specs returns every spec for that product matching ProductSpecResponse")]
    public async Task Get_ProductWithSpecs_ReturnsMatchingSpecs()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec List Probe Product", token);
        var specId = await CreateThrowawaySpecAsync(CreatedProductId, token, "Weight", "2.5", "kg");

        var response = await ApiContext.GetAsync($"/products/{CreatedProductId}/specs");

        Assert.That(response.Status, Is.EqualTo(200));
        var specs = (await response.JsonAsync())!.Value;
        Assert.That(specs.GetArrayLength(), Is.EqualTo(1));
        var spec = specs[0];
        Assert.That(spec.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(ProductSpecResponseProperties));
        Assert.That(spec.GetProperty("id").GetString(), Is.EqualTo(specId));
        Assert.That(spec.GetProperty("product_id").GetString(), Is.EqualTo(CreatedProductId));
        Assert.That(spec.GetProperty("spec_name").GetString(), Is.EqualTo("Weight"));
        Assert.That(spec.GetProperty("spec_value").GetString(), Is.EqualTo("2.5"));
        Assert.That(spec.GetProperty("spec_unit").GetString(), Is.EqualTo("kg"));
    }

    // ---- POST /products/{productId}/specs ----

    [Test]
    [AllureName("POST /products/{productId}/specs with no auth returns 401 Unauthorized")]
    public async Task Create_NoAuth_ReturnsUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Create Probe Product", token);

        var response = await CreateSpecAsync(CreatedProductId, "Weight", "1.5", "kg");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));
    }

    [Test]
    [AllureName("POST /products/{productId}/specs succeeds for any authenticated user, not just an admin")]
    public async Task Create_WithCustomerAuth_Succeeds()
    {
        var adminToken = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Create Probe Product", adminToken);
        var customerToken = await GetCustomerTokenAsync();

        var response = await CreateSpecAsync(CreatedProductId, "Material", "Steel", bearerToken: customerToken);

        Assert.That(response.Status, Is.EqualTo(201));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(ProductSpecResponseProperties));
        Assert.That(body.GetProperty("product_id").GetString(), Is.EqualTo(CreatedProductId));
        Assert.That(body.GetProperty("spec_name").GetString(), Is.EqualTo("Material"));
        Assert.That(body.GetProperty("spec_value").GetString(), Is.EqualTo("Steel"));
        Assert.That(body.GetProperty("spec_unit").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    [AllureName("POST /products/{productId}/specs with missing required fields returns 422 with Laravel's default {message, errors} shape - NOT the flat shape every FormRequest-backed resource uses")]
    public async Task Create_MissingRequiredFields_ReturnsUnprocessableEntity()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Validation Probe Product", token);

        // ProductSpecController::store() calls $request->validate([...]) inline rather than
        // through a BaseFormRequest subclass (unlike Product/Brand/Category). That matters on
        // the wire: BaseFormRequest::failedValidation() always throws a JSON 422/409 regardless
        // of the Accept header, but Laravel's *default* validation-failure handling only
        // returns JSON when the request declares Accept: application/json - otherwise it 302s
        // back (found by probing the live endpoint: a request without this header gets
        // redirected instead of a 422). A real browser-based consumer (Angular's HttpClient)
        // always sends this header, so this test does too, to exercise the path real traffic
        // actually takes.
        var response = await ApiContext.PostAsync($"/products/{CreatedProductId}/specs", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}",
                ["Accept"] = "application/json",
            },
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        // Laravel's own default ValidationException JSON shape - {message, errors: {field: [...]}}
        // - not the flat {field: [...]} shape BrandSpecTests/CategorySpecTests/ProductSpecTests
        // (the Product one) document for their own create validation failures.
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "message", "errors" }));
        var errors = body.GetProperty("errors");
        Assert.That(errors.GetProperty("spec_name")[0].GetString(), Is.EqualTo("The spec name field is required."));
        Assert.That(errors.GetProperty("spec_value")[0].GetString(), Is.EqualTo("The spec value field is required."));
    }

    // ---- GET /products/{productId}/specs/{specId} ----

    [Test]
    [AllureName("GET /products/{productId}/specs/{specId} returns 200 with the matching spec")]
    public async Task GetById_ExistingSpec_ReturnsOkWithSpecShape()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec GetById Probe Product", token);
        var specId = await CreateThrowawaySpecAsync(CreatedProductId, token);

        var response = await ApiContext.GetAsync($"/products/{CreatedProductId}/specs/{specId}");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(ProductSpecResponseProperties));
        Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(specId));
    }

    [Test]
    [AllureName("GET /products/{productId}/specs/{specId} returns 404 for a missing spec")]
    public async Task GetById_MissingSpec_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec GetById Probe Product", token);

        var response = await ApiContext.GetAsync($"/products/{CreatedProductId}/specs/does-not-exist");

        Assert.That(response.Status, Is.EqualTo(404));
    }

    [Test]
    [AllureName("GET /products/{productId}/specs/{specId} returns 404 when the spec belongs to a different product - the pairing is enforced")]
    public async Task GetById_SpecBelongsToDifferentProduct_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();
        var otherProductId = await CreateThrowawayProductAsync("Spec Mismatch Probe Product B", token);
        var otherSpecId = await CreateThrowawaySpecAsync(otherProductId, token);
        CreatedProductId = await CreateThrowawayProductAsync("Spec Mismatch Probe Product A", token);

        try
        {
            // ProductSpecController::show() scopes by BOTH product_id and id
            // (ProductSpec::where('product_id', $productId)->findOrFail($specId)) - a real spec
            // id fetched under the wrong product id must still 404, proving the API enforces
            // the product/spec pairing rather than treating spec ids as globally addressable.
            var response = await ApiContext.GetAsync($"/products/{CreatedProductId}/specs/{otherSpecId}");

            Assert.That(response.Status, Is.EqualTo(404));
        }
        finally
        {
            await DeleteProductAsync(otherProductId, token);
        }
    }

    // ---- PUT /products/{productId}/specs/{specId} ----

    [Test]
    [AllureName("PUT /products/{productId}/specs/{specId} with no auth returns 401 Unauthorized")]
    public async Task Update_NoAuth_ReturnsUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Update Probe Product", token);
        var specId = await CreateThrowawaySpecAsync(CreatedProductId, token);

        var response = await ApiContext.PutAsync($"/products/{CreatedProductId}/specs/{specId}", new APIRequestContextOptions
        {
            DataObject = new { spec_value = "Updated" },
        });

        Assert.That(response.Status, Is.EqualTo(401));
    }

    [Test]
    [AllureName("PUT /products/{productId}/specs/{specId} with a single field returns 200 {success:true}, and only touches that field")]
    public async Task Update_SingleField_OnlyChangesThatField()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Update Probe Product", token);
        var specId = await CreateThrowawaySpecAsync(CreatedProductId, token, "Weight", "1.5", "kg");
        var customerToken = await GetCustomerTokenAsync();

        var response = await ApiContext.PutAsync($"/products/{CreatedProductId}/specs/{specId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {customerToken}" },
            DataObject = new { spec_value = "3.0" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);

        var after = (await (await ApiContext.GetAsync($"/products/{CreatedProductId}/specs/{specId}")).JsonAsync())!.Value;
        Assert.That(after.GetProperty("spec_value").GetString(), Is.EqualTo("3.0"));
        Assert.That(after.GetProperty("spec_name").GetString(), Is.EqualTo("Weight"), "a field not sent in the update must not change");
        Assert.That(after.GetProperty("spec_unit").GetString(), Is.EqualTo("kg"), "a field not sent in the update must not change");
    }

    [Test]
    [AllureName("PUT /products/{productId}/specs/{specId} for a missing spec returns 404, even with valid auth")]
    public async Task Update_MissingSpec_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Update Probe Product", token);

        var response = await ApiContext.PutAsync($"/products/{CreatedProductId}/specs/does-not-exist", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new { spec_value = "Doesn't matter" },
        });

        Assert.That(response.Status, Is.EqualTo(404));
    }

    // ---- DELETE /products/{productId}/specs/{specId} ----

    [Test]
    [AllureName("DELETE /products/{productId}/specs/{specId} with no auth returns 401 Unauthorized")]
    public async Task Delete_NoAuth_ReturnsUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Delete Probe Product", token);
        var specId = await CreateThrowawaySpecAsync(CreatedProductId, token);

        var response = await ApiContext.DeleteAsync($"/products/{CreatedProductId}/specs/{specId}");

        Assert.That(response.Status, Is.EqualTo(401));
    }

    [Test]
    [AllureName("DELETE /products/{productId}/specs/{specId} with any authenticated user returns 204 and the spec is actually gone")]
    public async Task Delete_WithCustomerAuth_DeletesSpec()
    {
        var adminToken = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Delete Probe Product", adminToken);
        var specId = await CreateThrowawaySpecAsync(CreatedProductId, adminToken);
        var customerToken = await GetCustomerTokenAsync();

        var response = await DeleteSpecAsync(CreatedProductId, specId, customerToken);
        Assert.That(response.Status, Is.EqualTo(204));

        var reloaded = await ApiContext.GetAsync($"/products/{CreatedProductId}/specs/{specId}");
        Assert.That(reloaded.Status, Is.EqualTo(404));
    }

    [Test]
    [AllureName("DELETE /products/{productId}/specs/{specId} for a missing spec returns 204 regardless - unlike every other resource's delete, it never checks existence first")]
    public async Task Delete_MissingSpec_ReturnsNoContentRegardless()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Delete Probe Product", token);

        // ProductSpecController::destroy() runs a bare
        // ProductSpec::where('product_id', $productId)->where('id', $specId)->delete() and
        // always returns 204, never checking the affected-row count - unlike Brand/Category's
        // destroy() (404 for a missing id) or this resource's own show()/update() (findOrFail).
        // Verified live: deleting a spec id that was never created returns 204, not 404.
        var response = await DeleteSpecAsync(CreatedProductId, "does-not-exist", token);

        Assert.That(response.Status, Is.EqualTo(204));
    }

    // ---- GET /product-specs/names ----

    [Test]
    [AllureName("GET /product-specs/names returns 200 with a name/values/unit group per distinct spec name")]
    public async Task SpecNames_ReturnsOkWithGroupedShape()
    {
        var response = await ApiContext.GetAsync("/product-specs/names");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0), "expected at least one seeded spec name");

        foreach (var group in body.EnumerateArray())
        {
            Assert.That(group.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "name", "values", "unit" }));
            Assert.That(group.GetProperty("values").ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(group.GetProperty("values").GetArrayLength(), Is.GreaterThan(0));
        }
    }

    [Test]
    [AllureName("GET /product-specs/names reflects a spec created moments ago - its cache is invalidated on write")]
    public async Task SpecNames_ReflectsRecentlyCreatedSpec()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Names Probe Product", token);
        var uniqueSpecName = $"Contract Probe Spec {Guid.NewGuid():N}";
        await CreateSpecAsync(CreatedProductId, uniqueSpecName, "Yes", bearerToken: token);

        var response = await ApiContext.GetAsync("/product-specs/names");

        var body = (await response.JsonAsync())!.Value;
        var names = body.EnumerateArray().Select(g => g.GetProperty("name").GetString()).ToList();
        Assert.That(names, Does.Contain(uniqueSpecName),
            "ProductSpecController::store() calls Cache::forget('product_specs.names') - a new spec name should show up on the very next read");
    }
}
