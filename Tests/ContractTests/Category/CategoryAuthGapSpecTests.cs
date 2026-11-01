using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Category;

/// <summary>
/// Intentionally failing tests tracking a known security gap found while exploring
/// <c>POST</c>/<c>PUT /categories</c> (see <see cref="CategorySpecTests.Create_ValidCategory_NoAuth_ReturnsCreated"/>
/// and <see cref="CategorySpecTests.Put_ValidCategory_NoAuth_ReturnsSuccess"/>, which document
/// today's actual - unauthenticated-succeeds - behavior). Same gap, same reasoning, as
/// <c>Tests/ContractTests/Brand/BrandAuthGapSpecTests.cs</c>.
///
/// These assert the behavior <i>should</i> be, by analogy with <c>DELETE</c> (which genuinely
/// enforces <c>apiAuth</c>) and with how an admin-only write operation is normally expected to
/// behave: mutating a category without authentication should be rejected with 401. The OpenAPI
/// spec doesn't declare this requirement either way for <c>POST</c>/<c>PUT</c>, so this isn't a
/// documented contract being violated - it's a judgment call about what the contract <i>ought</i>
/// to say, made visible as a red test rather than left as a silent assumption.
///
/// Remove or flip these once the gap is fixed (or confirmed intentional) - don't let them sit
/// red indefinitely without someone deciding.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Category")]
[AllureSubSuite("CategoryAuthGapSpecTests")]
public class CategoryAuthGapSpecTests : CategoryContractTestBase
{
    [Test]
    [AllureName("KNOWN GAP: POST /categories without auth should return 401, like DELETE does")]
    public async Task Create_NoAuth_ShouldBeUnauthorized()
    {
        var response = await ApiContext.PostAsync("/categories", new APIRequestContextOptions
        {
            DataObject = new { name = "Auth Gap Probe", slug = $"auth-gap-probe-{Guid.NewGuid():N}" },
        });

        if (response.Status == 201)
        {
            // The gap: it was created despite no auth. Clean up so this test doesn't leak data
            // on every run while it's still red.
            var createdBody = (await response.JsonAsync())!.Value;
            CreatedCategoryId = createdBody.GetProperty("id").GetString();
        }

        Assert.That(response.Status, Is.EqualTo(401), "POST /categories should require auth, matching DELETE - currently it does not");
    }

    [Test]
    [AllureName("KNOWN GAP: PUT /categories/{id} without auth should return 401, like DELETE does")]
    public async Task Update_NoAuth_ShouldBeUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedCategoryId = await CreateThrowawayCategoryAsync("Auth Gap Probe", bearerToken: token);

        var response = await ApiContext.PutAsync($"/categories/{CreatedCategoryId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Updated Without Auth", slug = $"auth-gap-probe-{Guid.NewGuid():N}" },
        });

        Assert.That(response.Status, Is.EqualTo(401), "PUT /categories/{id} should require auth, matching DELETE - currently it does not");
    }

    [Test]
    [AllureName("KNOWN GAP: PATCH /categories/{id} without auth should return 401, like DELETE does")]
    public async Task Patch_NoAuth_ShouldBeUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedCategoryId = await CreateThrowawayCategoryAsync("Auth Gap Probe", bearerToken: token);

        var response = await ApiContext.PatchAsync($"/categories/{CreatedCategoryId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Patched Without Auth" },
        });

        Assert.That(response.Status, Is.EqualTo(401), "PATCH /categories/{id} should require auth, matching DELETE - currently it does not");
    }
}
