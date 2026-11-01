using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.ProductSpec;

/// <summary>
/// Shared setup/teardown for product-spec contract tests: login, throwaway-product creation (a
/// spec cannot exist without a parent product), spec creation against it, and automatic cleanup
/// via <see cref="CreatedProductId"/>. Mirrors
/// <c>Tests/ContractTests/Brand/BrandContractTestBase.cs</c> in spirit, but teardown deletes the
/// parent <em>product</em>, not a spec directly - <c>product_specs</c> has an
/// <c>onDelete('cascade')</c> foreign key to <c>products</c> (see
/// <c>database/migrations/2026_04_09_000000_create_product_specs_table.php</c> in the API repo),
/// so deleting the throwaway product also removes every spec created under it in one call. This
/// is itself part of the Product/ProductSpec contract - see
/// <c>ProductSpecPactTests.DeletingProduct_CascadesToItsSpecs</c>.
/// </summary>
public abstract class ProductSpecContractTestBase : ApiTest
{
    /// <summary>
    /// Set this from a test (or a derived [SetUp]) when a throwaway product was created and
    /// needs deleting afterward - this cascades away any specs created under it too. Left null,
    /// nothing runs - tests that don't create a product don't pay for teardown they don't need.
    /// </summary>
    protected string? CreatedProductId;

    protected Task<string> GetAdminTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Admin);

    /// <summary>
    /// Logs in as a plain, non-admin seeded customer - <c>ProductSpecController</c> gates
    /// writes with <c>auth:users</c>, not <c>role:admin</c> (unlike every other resource's
    /// destroy in this suite), so any authenticated user is expected to succeed here.
    /// </summary>
    protected Task<string> GetCustomerTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Customer);

    protected async Task<(string CategoryId, string BrandId, string ImageId)> GetValidProductDependenciesAsync()
    {
        var categoryId = (await (await ApiContext.GetAsync("/categories")).JsonAsync())!.Value[0].GetProperty("id").GetString()!;
        var brandId = (await (await ApiContext.GetAsync("/brands")).JsonAsync())!.Value[0].GetProperty("id").GetString()!;
        var sampleProduct = (await (await ApiContext.GetAsync("/products")).JsonAsync())!.Value.GetProperty("data")[0];
        var imageId = sampleProduct.GetProperty("product_image").GetProperty("id").GetString()!;
        return (categoryId, brandId, imageId);
    }

    protected async Task<string> CreateThrowawayProductAsync(string namePrefix, string bearerToken)
    {
        var (categoryId, brandId, imageId) = await GetValidProductDependenciesAsync();

        var response = await ApiContext.PostAsync("/products", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
            DataObject = new
            {
                name = namePrefix,
                description = "Created for product-spec contract test setup",
                price = 9.99,
                category_id = categoryId,
                brand_id = brandId,
                product_image_id = imageId,
                is_location_offer = false,
                is_rental = false,
            },
        });
        return (await response.JsonAsync())!.Value.GetProperty("id").GetString()!;
    }

    protected Task<IAPIResponse> CreateSpecAsync(string productId, string specName, string specValue, string? specUnit = null, string? bearerToken = null)
    {
        var options = new APIRequestContextOptions
        {
            DataObject = new { spec_name = specName, spec_value = specValue, spec_unit = specUnit },
        };
        if (bearerToken is not null)
        {
            options.Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" };
        }

        return ApiContext.PostAsync($"/products/{productId}/specs", options);
    }

    protected async Task<string> CreateThrowawaySpecAsync(string productId, string bearerToken, string specName = "Weight", string specValue = "1.5", string? specUnit = "kg")
    {
        var response = await CreateSpecAsync(productId, specName, specValue, specUnit, bearerToken);
        return (await response.JsonAsync())!.Value.GetProperty("id").GetString()!;
    }

    protected Task<IAPIResponse> DeleteSpecAsync(string productId, string specId, string bearerToken) =>
        ApiContext.DeleteAsync($"/products/{productId}/specs/{specId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
        });

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
