using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Category;

/// <summary>
/// Shared setup/teardown for category contract tests: login, throwaway-category creation, and
/// automatic cleanup via <see cref="CreatedCategoryId"/>. Mirrors
/// <c>Tests/ContractTests/Brand/BrandContractTestBase.cs</c>.
/// </summary>
public abstract class CategoryContractTestBase : ApiTest
{
    /// <summary>
    /// Set this from a test (or a derived [SetUp]) when a throwaway category was created and
    /// needs deleting afterward. Left null, nothing runs - tests that don't create a category
    /// don't pay for teardown they don't need. For a test that creates more than one category
    /// (e.g. a parent/child pair), delete the extra ones yourself and leave only the last one
    /// for this field, or clean up manually via try/finally - see
    /// <c>CategorySpecTests.Delete_CategoryWithSubCategory_ReturnsConflict</c>.
    /// </summary>
    protected string? CreatedCategoryId;

    protected Task<string> GetAdminTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Admin);

    protected async Task<string> CreateThrowawayCategoryAsync(string namePrefix, string? parentId = null, string? bearerToken = null)
    {
        var body = parentId is null
            ? new { name = namePrefix, slug = $"{namePrefix.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}" }
            : (object)new { name = namePrefix, slug = $"{namePrefix.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}", parent_id = parentId };

        var options = new APIRequestContextOptions { DataObject = body };
        if (bearerToken is not null)
        {
            options.Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" };
        }

        var response = await ApiContext.PostAsync("/categories", options);
        return (await response.JsonAsync())!.Value.GetProperty("id").GetString()!;
    }

    protected Task<IAPIResponse> DeleteCategoryAsync(string categoryId, string bearerToken) =>
        ApiContext.DeleteAsync($"/categories/{categoryId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
        });

    [TearDown]
    public async Task DeleteCreatedCategoryAsync()
    {
        if (CreatedCategoryId is null)
        {
            return;
        }

        var token = await GetAdminTokenAsync();
        await DeleteCategoryAsync(CreatedCategoryId, token);
    }
}
