using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Favorite;

/// <summary>
/// Shared setup/teardown for favorite contract tests: customer login, throwaway-product creation
/// (favorites reference a product, so most tests need one), throwaway-favorite creation, and
/// automatic cleanup. Mirrors <c>Tests/ContractTests/Category/CategoryContractTestBase.cs</c>.
/// </summary>
public abstract class FavoriteContractTestBase : ApiTest
{
    /// <summary>
    /// Set this from a test when a throwaway favorite was created and needs deleting afterward.
    /// Deleted with <see cref="CreatedFavoriteOwnerToken"/> (favorites are owner-scoped - deleting
    /// with the wrong token is a silent no-op, see <c>FavoriteGapSpecTests</c>), which must be set
    /// alongside it.
    /// </summary>
    protected string? CreatedFavoriteId;
    protected string? CreatedFavoriteOwnerToken;

    /// <summary>
    /// Set this from a test when a throwaway product was created and needs deleting afterward.
    /// </summary>
    protected string? CreatedProductId;

    protected Task<string> GetAdminTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Admin);
    protected Task<string> GetCustomerTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Customer);
    protected Task<string> GetCustomer2TokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Customer2);

    /// <summary>
    /// Creates a throwaway product to favorite. Favorites don't need a *unique* product (no
    /// exclusivity besides the per-user uniqueness enforced in <c>Create_DuplicateFavorite_ReturnsConflict</c>),
    /// but a dedicated one keeps tests from colliding with each other over the same product_id
    /// while running in parallel across files - see the parallel-execution note in AGENTS.md.
    /// </summary>
    protected async Task<string> CreateThrowawayProductAsync(string namePrefix)
    {
        var categoryId = (await (await ApiContext.GetAsync("/categories")).JsonAsync())!.Value[0].GetProperty("id").GetString();
        var brandId = (await (await ApiContext.GetAsync("/brands")).JsonAsync())!.Value[0].GetProperty("id").GetString();
        var sampleProduct = (await (await ApiContext.GetAsync("/products")).JsonAsync())!.Value.GetProperty("data")[0];
        var imageId = sampleProduct.GetProperty("product_image").GetProperty("id").GetString();

        var response = await ApiContext.PostAsync("/products", new APIRequestContextOptions
        {
            DataObject = new
            {
                name = $"{namePrefix} {Guid.NewGuid():N}",
                description = "created for favorite contract tests",
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

    protected Task<IAPIResponse> DeleteProductAsync(string productId, string adminToken) =>
        ApiContext.DeleteAsync($"/products/{productId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" },
        });

    protected async Task<string> CreateThrowawayFavoriteAsync(string bearerToken, string productId)
    {
        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
            DataObject = new { product_id = productId },
        });

        return (await response.JsonAsync())!.Value.GetProperty("id").GetString()!;
    }

    protected Task<IAPIResponse> DeleteFavoriteAsync(string favoriteId, string bearerToken) =>
        ApiContext.DeleteAsync($"/favorites/{favoriteId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
        });

    [TearDown]
    public async Task DeleteCreatedFavoriteAndProductAsync()
    {
        if (CreatedFavoriteId is not null && CreatedFavoriteOwnerToken is not null)
        {
            await DeleteFavoriteAsync(CreatedFavoriteId, CreatedFavoriteOwnerToken);
        }

        if (CreatedProductId is not null)
        {
            var adminToken = await GetAdminTokenAsync();
            await DeleteProductAsync(CreatedProductId, adminToken);
        }
    }
}
