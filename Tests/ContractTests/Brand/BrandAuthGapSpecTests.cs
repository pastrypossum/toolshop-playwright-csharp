using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Brand;

/// <summary>
/// Intentionally failing tests tracking a known security gap found while exploring
/// <c>POST</c>/<c>PUT /brands</c> (see <see cref="BrandSpecTests.Create_ValidBrand_NoAuth_ReturnsCreated"/>
/// and <see cref="BrandSpecTests.Put_ValidBrand_NoAuth_ReturnsSuccess"/>, which document today's
/// actual - unauthenticated-succeeds - behavior).
///
/// These assert the behavior <i>should</i> be, by analogy with <c>DELETE</c> (which genuinely
/// enforces <c>apiAuth</c>) and with how an admin-only write operation is normally expected to
/// behave: mutating a brand without authentication should be rejected with 401. The OpenAPI
/// spec doesn't declare this requirement either way for <c>POST</c>/<c>PUT</c>, so this isn't a
/// documented contract being violated - it's a judgment call about what the contract <i>ought</i>
/// to say, made visible as a red test rather than left as a silent assumption.
///
/// Remove or flip these once the gap is fixed (or confirmed intentional) - don't let them sit
/// red indefinitely without someone deciding.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Brand")]
[AllureSubSuite("BrandAuthGapSpecTests")]
public class BrandAuthGapSpecTests : BrandContractTestBase
{
    [Test]
    [AllureName("KNOWN GAP: POST /brands without auth should return 401, like DELETE does")]
    public async Task Create_NoAuth_ShouldBeUnauthorized()
    {
        var response = await ApiContext.PostAsync("/brands", new APIRequestContextOptions
        {
            DataObject = new { name = "Auth Gap Probe", slug = $"auth-gap-probe-{Guid.NewGuid():N}" },
        });

        if (response.Status == 201)
        {
            // The gap: it was created despite no auth. Clean up so this test doesn't leak data
            // on every run while it's still red.
            var createdBody = (await response.JsonAsync())!.Value;
            CreatedBrandId = createdBody.GetProperty("id").GetString();
        }

        Assert.That(response.Status, Is.EqualTo(401), "POST /brands should require auth, matching DELETE - currently it does not");
    }

    [Test]
    [AllureName("KNOWN GAP: PUT /brands/{id} without auth should return 401, like DELETE does")]
    public async Task Update_NoAuth_ShouldBeUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        CreatedBrandId = await CreateThrowawayBrandAsync("Auth Gap Probe", token);

        var response = await ApiContext.PutAsync($"/brands/{CreatedBrandId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Updated Without Auth", slug = $"auth-gap-probe-{Guid.NewGuid():N}" },
        });

        Assert.That(response.Status, Is.EqualTo(401), "PUT /brands/{id} should require auth, matching DELETE - currently it does not");
    }
}
