using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Brand;

/// <summary>
/// Shared setup/teardown for brand contract tests: login, throwaway-brand creation, and
/// automatic cleanup via <see cref="CreatedBrandId"/>. Extracted after the same
/// login-then-delete pattern turned up near-identically across five test files - past the
/// point where duplicating it was still the simpler option.
/// </summary>
public abstract class BrandContractTestBase : ApiTest
{
    /// <summary>
    /// Set this from a test (or a derived [SetUp]) when a throwaway brand was created and
    /// needs deleting afterward. Left null, nothing runs - tests that don't create a brand
    /// don't pay for teardown they don't need.
    /// </summary>
    protected string? CreatedBrandId;

    protected Task<string> GetAdminTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Admin);

    protected async Task<string> CreateThrowawayBrandAsync(string namePrefix, string? bearerToken = null)
    {
        var options = new APIRequestContextOptions
        {
            DataObject = new { name = namePrefix, slug = $"{namePrefix.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}" },
        };
        if (bearerToken is not null)
        {
            options.Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" };
        }

        var response = await ApiContext.PostAsync("/brands", options);
        return (await response.JsonAsync())!.Value.GetProperty("id").GetString()!;
    }

    protected Task<IAPIResponse> DeleteBrandAsync(string brandId, string bearerToken) =>
        ApiContext.DeleteAsync($"/brands/{brandId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
        });

    [TearDown]
    public async Task DeleteCreatedBrandAsync()
    {
        if (CreatedBrandId is null)
        {
            return;
        }

        var token = await GetAdminTokenAsync();
        await DeleteBrandAsync(CreatedBrandId, token);
    }
}
