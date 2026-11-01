using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Product;

/// <summary>
/// Intentionally failing tests tracking a known security gap found while exploring
/// <c>POST</c>/<c>PUT</c>/<c>PATCH /products</c> (see
/// <see cref="ProductSpecTests.Create_ValidProduct_NoAuth_ReturnsCreated"/>,
/// <see cref="ProductSpecTests.Put_ValidProduct_NoAuth_ReturnsSuccess"/> and
/// <see cref="ProductSpecTests.Patch_SingleField_OnlyChangesThatField"/>, which document today's
/// actual - unauthenticated-succeeds - behavior). Same gap, same reasoning, as
/// <c>Tests/ContractTests/Brand/BrandAuthGapSpecTests.cs</c> and
/// <c>Tests/ContractTests/Category/CategoryAuthGapSpecTests.cs</c>.
///
/// Notably <c>DELETE /products/{id}</c> does NOT have this gap - it's the one write operation
/// genuinely gated by <c>role:admin</c> (see <see cref="ProductSpecTests.Delete_NoAuth_ReturnsUnauthorized"/>).
/// These assert the behavior <i>should</i> be, by analogy with <c>DELETE</c>: mutating a product
/// without authentication should be rejected with 401. The OpenAPI spec doesn't declare this
/// requirement either way for <c>POST</c>/<c>PUT</c>/<c>PATCH</c>, so this isn't a documented
/// contract being violated - it's a judgment call about what the contract <i>ought</i> to say,
/// made visible as a red test rather than left as a silent assumption.
///
/// Remove or flip these once the gap is fixed (or confirmed intentional) - don't let them sit
/// red indefinitely without someone deciding.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Product")]
[AllureSubSuite("ProductAuthGapSpecTests")]
public class ProductAuthGapSpecTests : ProductContractTestBase
{
    [Test]
    [AllureName("KNOWN GAP: POST /products without auth should return 401, like DELETE does")]
    public async Task Create_NoAuth_ShouldBeUnauthorized()
    {
        var (categoryId, brandId, imageId) = await GetValidProductDependenciesAsync();

        var response = await ApiContext.PostAsync("/products", new APIRequestContextOptions
        {
            DataObject = new
            {
                name = "Auth Gap Probe Product",
                description = "probe",
                price = 9.99,
                category_id = categoryId,
                brand_id = brandId,
                product_image_id = imageId,
                is_location_offer = false,
                is_rental = false,
            },
        });

        if (response.Status == 201)
        {
            // The gap: it was created despite no auth. Clean up so this test doesn't leak data
            // on every run while it's still red.
            var createdBody = (await response.JsonAsync())!.Value;
            CreatedProductId = createdBody.GetProperty("id").GetString();
        }

        Assert.That(response.Status, Is.EqualTo(401), "POST /products should require auth, matching DELETE - currently it does not");
    }

    [Test]
    [AllureName("KNOWN GAP: PUT /products/{id} without auth should return 401, like DELETE does")]
    public async Task Update_NoAuth_ShouldBeUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Auth Gap Probe Product", token);

        var response = await ApiContext.PutAsync($"/products/{CreatedProductId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Updated Without Auth" },
        });

        Assert.That(response.Status, Is.EqualTo(401), "PUT /products/{id} should require auth, matching DELETE - currently it does not");
    }

    [Test]
    [AllureName("KNOWN GAP: PATCH /products/{id} without auth should return 401, like DELETE does")]
    public async Task Patch_NoAuth_ShouldBeUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Auth Gap Probe Product", token);

        var response = await ApiContext.PatchAsync($"/products/{CreatedProductId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Patched Without Auth" },
        });

        Assert.That(response.Status, Is.EqualTo(401), "PATCH /products/{id} should require auth, matching DELETE - currently it does not");
    }
}
