using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Product;

/// <summary>
/// Shared setup/teardown for product contract tests: login, throwaway-product creation (with
/// a valid category/brand/image dependency trio resolved from seeded data), and automatic
/// cleanup via <see cref="CreatedProductId"/>. Mirrors
/// <c>Tests/ContractTests/Brand/BrandContractTestBase.cs</c>; <c>destroy</c> genuinely requires
/// admin auth here (<c>role:admin</c> middleware), unlike Brand/Category where only
/// <c>DELETE</c> enforces auth - see <see cref="ProductAuthGapSpecTests"/> for the write-side gap.
/// </summary>
public abstract class ProductContractTestBase : ApiTest
{
    /// <summary>
    /// Set this from a test (or a derived [SetUp]) when a throwaway product was created and
    /// needs deleting afterward. Left null, nothing runs - tests that don't create a product
    /// don't pay for teardown they don't need.
    /// </summary>
    protected string? CreatedProductId;

    protected Task<string> GetAdminTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Admin);

    /// <summary>
    /// Resolves a valid (category_id, brand_id, product_image_id) trio from the live seeded
    /// data - every create/update payload needs real ids for these, there is no throwaway way
    /// to invent them.
    /// </summary>
    protected async Task<(string CategoryId, string BrandId, string ImageId)> GetValidProductDependenciesAsync()
    {
        var categoryId = (await (await ApiContext.GetAsync("/categories")).JsonAsync())!.Value[0].GetProperty("id").GetString()!;
        var brandId = (await (await ApiContext.GetAsync("/brands")).JsonAsync())!.Value[0].GetProperty("id").GetString()!;
        var sampleProduct = (await (await ApiContext.GetAsync("/products")).JsonAsync())!.Value.GetProperty("data")[0];
        var imageId = sampleProduct.GetProperty("product_image").GetProperty("id").GetString()!;
        return (categoryId, brandId, imageId);
    }

    protected async Task<string> CreateThrowawayProductAsync(string namePrefix, string? bearerToken = null, bool isRental = false)
    {
        var (categoryId, brandId, imageId) = await GetValidProductDependenciesAsync();

        var options = new APIRequestContextOptions
        {
            DataObject = new
            {
                name = namePrefix,
                description = "Created for contract test setup",
                price = 9.99,
                category_id = categoryId,
                brand_id = brandId,
                product_image_id = imageId,
                is_location_offer = false,
                is_rental = isRental,
            },
        };
        if (bearerToken is not null)
        {
            options.Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" };
        }

        var response = await ApiContext.PostAsync("/products", options);
        return (await response.JsonAsync())!.Value.GetProperty("id").GetString()!;
    }

    protected Task<IAPIResponse> DeleteProductAsync(string productId, string bearerToken) =>
        ApiContext.DeleteAsync($"/products/{productId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
        });

    [TearDown]
    public async Task DeleteCreatedProductAsync()
    {
        if (CreatedProductId is null)
        {
            return;
        }

        var token = await GetAdminTokenAsync();
        await DeleteProductAsync(CreatedProductId, token);
    }
}
