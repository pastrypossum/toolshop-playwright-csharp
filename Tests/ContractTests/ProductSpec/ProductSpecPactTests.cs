using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.ProductSpec;

/// <summary>
/// Contract tests for the Product/ProductSpec relationship itself - the layer on top of
/// <see cref="ProductSpecSpecTests"/>'s and
/// <see cref="Toolshop.Playwright.Tests.Tests.ContractTests.Product.ProductSpecTests"/>'s
/// single-resource checks. Per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Pact-track convention this should be built
/// from a real consumer's traced behaviour rather than hand-written assertions about the
/// endpoint in isolation - no local Angular dev server was reachable here to capture a fixture
/// with <c>playwright-cli</c>, so each test below instead cites the exact frontend source it
/// was traced from (same approach <c>BrandPactTests.Create_ValidationFailure_ReturnsFieldKeyedErrors</c>
/// and its sibling take when no fixture is involved), plus the one case
/// (<see cref="DeletingProduct_CascadesToItsSpecs"/>) traced from the API's own migration rather
/// than the frontend.
///
/// Real consumers traced:
/// <list type="bullet">
/// <item><c>admin/products-add-edit/products-add-edit.component.ts</c> - loads a product by id,
/// then calls <c>ProductSpecService.getSpecs(this.id)</c> and
/// <c>ProductSpecService.createSpec(this.id, newSpec)</c>, pushing the create response straight
/// into the on-screen list (<c>addSpec()</c>, line ~205-213).</item>
/// <item><c>products/category/category.component.ts</c>'s spec-filter sidebar: populated from
/// <c>ProductSpecService.getSpecNames()</c> (<c>/product-specs/names</c>), and
/// <c>filterBySpec()</c>/<c>buildSpecFilterString()</c> (line ~298-329) turn the checked
/// name/value pairs into a <c>by_spec=name:value1|value2,...</c> query sent to
/// <c>ProductService.getProductsByCategoryAndBrand()</c> -&gt; <c>GET /products</c>
/// (<c>_services/product.service.ts</c> line ~70).</item>
/// <item><c>database/migrations/2026_04_09_000000_create_product_specs_table.php</c>: the
/// <c>product_id</c> foreign key is declared <c>onDelete('cascade')</c>, not the default
/// restrict every other FK onto <c>products</c> uses (<c>cart_items</c>, <c>favorites</c>,
/// <c>invoice_items</c>) - deleting a product silently takes its specs with it rather than the
/// 409-conflict shape <c>Delete_BrandInUse_ReturnsConflict</c>'s analogues exercise.</item>
/// </list>
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("ProductSpec")]
[AllureSubSuite("ProductSpecPactTests")]
public class ProductSpecPactTests : ProductSpecContractTestBase
{
    [Test]
    [AllureName("A spec created under a product id is reflected both by its own endpoint and nested inside GET /products/{id}, as the admin edit page relies on")]
    public async Task SpecCreatedUnderProduct_AppearsNestedInProductShowResponse()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Relationship Probe Product", token);

        // Mirrors products-add-edit.component.ts's addSpec(): the product id from the page the
        // admin is already on is sent straight to the specs sub-resource - the cross-endpoint
        // reference this test exists to cover.
        var specId = await CreateThrowawaySpecAsync(CreatedProductId, token, "Weight", "123", "g");

        var specsResponse = await ApiContext.GetAsync($"/products/{CreatedProductId}/specs");
        var specs = (await specsResponse.JsonAsync())!.Value;
        Assert.That(specs.GetArrayLength(), Is.EqualTo(1));
        Assert.That(specs[0].GetProperty("id").GetString(), Is.EqualTo(specId));

        // The comparison feature's GraphQL query (_services/comparison.service.ts) and the
        // product detail page both expect a product's own specs to travel bundled with it, not
        // just via the dedicated specs endpoint - GET /products/{id} genuinely does this (see
        // ProductSpecTests.GetById_ExistingProduct_IncludesUndocumentedSpecsArray for the shape
        // assertion); here the point is that the specific spec just created shows up there too.
        var productResponse = await ApiContext.GetAsync($"/products/{CreatedProductId}");
        var nestedSpecs = (await productResponse.JsonAsync())!.Value.GetProperty("specs");
        Assert.That(nestedSpecs.GetArrayLength(), Is.EqualTo(1));
        Assert.That(nestedSpecs[0].GetProperty("id").GetString(), Is.EqualTo(specId));
        Assert.That(nestedSpecs[0].GetProperty("spec_name").GetString(), Is.EqualTo("Weight"));
    }

    [Test]
    [AllureName("A spec_name/spec_value pair from GET /product-specs/names filters GET /products via by_spec, as the category page's filter sidebar relies on")]
    public async Task SpecNameValuePair_FiltersProductsViaBySpecQueryParam()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Filter Probe Product", token);
        var uniqueSpecName = $"Contract Probe Spec {Guid.NewGuid():N}";
        await CreateSpecAsync(CreatedProductId, uniqueSpecName, "Yes", bearerToken: token);

        // buildSpecFilterString() in category.component.ts joins checked name/value pairs as
        // "name:value1|value2,name2:value3" - a single pair is simply "name:value".
        var response = await ApiContext.GetAsync($"/products?by_spec={Uri.EscapeDataString(uniqueSpecName)}:Yes");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        var ids = body.GetProperty("data").EnumerateArray().Select(p => p.GetProperty("id").GetString()).ToList();
        Assert.That(ids, Does.Contain(CreatedProductId),
            "by_spec should filter /products down to (at least) the product carrying that exact spec_name/spec_value pair");
    }

    [Test]
    [AllureName("DELETE /products/{id} cascades to its specs - product_specs' FK is onDelete('cascade'), unlike every other FK onto products")]
    public async Task DeletingProduct_CascadesToItsSpecs()
    {
        var token = await GetAdminTokenAsync();
        var productId = await CreateThrowawayProductAsync("Cascade Delete Probe Product", token);
        await CreateThrowawaySpecAsync(productId, token, "Weight", "1.5", "kg");

        var response = await DeleteProductAsync(productId, token);
        Assert.That(response.Status, Is.EqualTo(204));

        // ProductSpecController::index() never checks the parent product exists - it just
        // queries product_specs by product_id - so the real signal of a cascade (vs. an
        // orphaned row left behind) is that this now returns an empty array, not a 404 and not
        // a non-empty array.
        var specsResponse = await ApiContext.GetAsync($"/products/{productId}/specs");
        Assert.That(specsResponse.Status, Is.EqualTo(200));
        var specs = (await specsResponse.JsonAsync())!.Value;
        Assert.That(specs.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(specs.GetArrayLength(), Is.EqualTo(0));

        // CreatedProductId left unset deliberately - the product is already gone, and deleting
        // it again in TearDown would just be a harmless 404, but there is nothing left to clean.
    }
}
