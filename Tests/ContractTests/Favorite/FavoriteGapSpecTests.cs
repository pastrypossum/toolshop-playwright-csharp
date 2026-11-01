using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Favorite;

/// <summary>
/// Intentionally failing tests tracking known gaps found while exploring <c>/favorites</c> -
/// same reasoning and same file-per-known-gap convention as
/// <c>Tests/ContractTests/Category/CategoryAuthGapSpecTests.cs</c>, but these two gaps are about
/// missing object-level ownership checks rather than missing authentication (every
/// <c>/favorites</c> operation already requires a valid bearer token - see
/// <see cref="FavoriteSpecTests"/> - so there's no authn gap to track here).
///
/// Both were found by probing the live endpoint, not from the OpenAPI spec (which doesn't speak
/// to ownership at all):
///
/// <see cref="GetById_NotOwned_ShouldReturnNotFound"/>: an IDOR. <c>FavoriteController::show()</c>
/// (<c>FavoriteService::getFavoriteById()</c>) does <c>Favorite::findOrFail($id)</c> with no
/// <c>user_id</c> filter at all - any authenticated user can read any other user's favorite by
/// id, unlike <c>index()</c> (which does filter by <c>Auth::id()</c> - see
/// <see cref="FavoriteSpecTests.Get_OnlyReturnsOwnFavorites"/>) and <c>destroy()</c> (which does
/// too, see below). Asserted here by analogy with those two siblings in the same controller.
///
/// <see cref="Delete_NotOwned_ShouldReturnNotFound"/> and
/// <see cref="Delete_MissingFavorite_ShouldReturnNotFound"/>: <c>FavoriteService::deleteFavorite()</c>
/// does filter by <c>user_id</c>, so a non-owner can't actually delete someone else's favorite
/// (no data-loss bug) - but <c>destroy()</c> returns 204 unconditionally, ignoring whether the
/// scoped delete actually matched a row. The live endpoint returns 204 identically whether the id
/// belongs to someone else, or doesn't exist at all, in both cases silently deleting nothing.
/// Every other delete-by-id endpoint in this app (<c>DELETE /categories/{id}</c>,
/// <c>DELETE /brands/{id}</c>) returns 404 for a missing/inaccessible resource - this is the odd
/// one out, and the spec itself documents 404 (<c>ItemNotFoundResponse</c>) as a possible
/// response here too.
///
/// Remove or flip these once the gaps are fixed (or confirmed intentional) - don't let them sit
/// red indefinitely without someone deciding.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Favorite")]
[AllureSubSuite("FavoriteGapSpecTests")]
public class FavoriteGapSpecTests : FavoriteContractTestBase
{
    [Test]
    [AllureName("KNOWN GAP (IDOR): GET /favorites/{id} for a favorite owned by another user should return 404, like the list endpoint's filtering implies")]
    public async Task GetById_NotOwned_ShouldReturnNotFound()
    {
        var ownerToken = await GetCustomerTokenAsync();
        var otherUserToken = await GetCustomer2TokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Gap Probe IDOR Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(ownerToken, CreatedProductId);
        CreatedFavoriteOwnerToken = ownerToken;

        var response = await ApiContext.GetAsync($"/favorites/{CreatedFavoriteId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {otherUserToken}" },
        });

        Assert.That(response.Status, Is.EqualTo(404),
            "GET /favorites/{id} should not expose another user's favorite - currently it returns 200 (IDOR)");
    }

    [Test]
    [AllureName("KNOWN GAP: DELETE /favorites/{id} for a favorite owned by another user should return 404, not a no-op 204")]
    public async Task Delete_NotOwned_ShouldReturnNotFound()
    {
        var ownerToken = await GetCustomerTokenAsync();
        var otherUserToken = await GetCustomer2TokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Gap Probe Delete NotOwned Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(ownerToken, CreatedProductId);
        CreatedFavoriteOwnerToken = ownerToken;

        var response = await DeleteFavoriteAsync(CreatedFavoriteId, otherUserToken);

        Assert.That(response.Status, Is.EqualTo(404),
            "DELETE /favorites/{id} for a favorite you don't own should return 404 - currently it silently no-ops and returns 204");

        // Prove it really was a no-op, independent of the status code assertion above.
        var reloaded = await ApiContext.GetAsync($"/favorites/{CreatedFavoriteId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {ownerToken}" },
        });
        Assert.That(reloaded.Status, Is.EqualTo(200), "the favorite should still exist - the non-owner's delete must not have actually removed it");
    }

    [Test]
    [AllureName("KNOWN GAP: DELETE /favorites/{id} for a missing favorite should return 404, like every other delete-by-id endpoint in this app")]
    public async Task Delete_MissingFavorite_ShouldReturnNotFound()
    {
        var token = await GetCustomerTokenAsync();

        var response = await DeleteFavoriteAsync("does-not-exist", token);

        Assert.That(response.Status, Is.EqualTo(404),
            "DELETE /favorites/{id} for a missing favorite should return 404, matching DELETE /categories/{id} and /brands/{id} - currently it returns 204");
    }
}
