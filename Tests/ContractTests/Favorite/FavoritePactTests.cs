using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Favorite;

/// <summary>
/// Contract tests built from real consumer dependencies rather than the OpenAPI spec alone - the
/// layer on top of <see cref="FavoriteSpecTests"/>'s specification-based checks. One file per
/// resource, covering every consumer dependency found for it, per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Pact-track convention.
///
/// Like <c>Tests/ContractTests/Category/CategoryPactTests.cs</c>, these dependencies were traced
/// by reading the Angular frontend source directly (available alongside this repo at
/// <c>practice-software-testing/sprint5/UI/src/app</c>) rather than recording with
/// <c>playwright-cli</c>.
///
/// <see cref="List_FavoriteItem_HasProductFieldsTheTemplateRenders"/>: traced from
/// <c>account/favorites/favorites.component.html</c>, which renders
/// <c>favorite.product.product_image.file_name</c> (into an <c>img src</c>),
/// <c>favorite.product.name</c> and <c>favorite.product.description</c> (through a
/// <c>truncate</c> pipe) directly from a <c>GET /favorites</c> list item - the spec's
/// <c>FavoriteWithProductResponse</c> only promises a <c>product</c> key exists
/// (<see cref="FavoriteSpecTests.Get_WithAuth_ReturnsOkWithFavoriteWithProductShape"/>), not that
/// these specific nested fields are populated.
///
/// <see cref="Create_DuplicateFavorite_MatchesConsumersErrorStringCheck"/> and
/// <see cref="Create_NoAuth_MatchesConsumersErrorStringCheck"/>: traced from
/// <c>products/detail/detail.component.ts</c>'s <c>addToFavorites()</c>, whose error callback
/// does <c>if (response.error.message === 'Duplicate Entry') ... else if (response.error.message
/// === 'Unauthorized')</c> - an exact string comparison, not just "some error occurred". This is
/// exactly the kind of client-side branching the Pact track exists to protect: the spec declares
/// status codes (409, 401) but says nothing about the message text those branches key off.
///
/// <see cref="Delete_ThenReloadingTheList_NoLongerIncludesIt"/>: traced from
/// <c>account/favorites/favorites.component.ts</c>'s <c>deleteFavorite()</c>, which on success
/// calls <c>loadFavorites()</c> - i.e. re-fetches via <c>GET /favorites</c> (the list), not
/// <c>GET /favorites/{id}</c>. <see cref="FavoriteSpecTests.Delete_WithAuth_DeletesFavorite"/>
/// already proves the single-item route 404s afterward; this proves the actual consumer-facing
/// path (the list the UI re-renders from) reflects the deletion too.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Favorite")]
[AllureSubSuite("FavoritePactTests")]
public class FavoritePactTests : FavoriteContractTestBase
{
    [Test]
    [AllureName("A GET /favorites list item's nested product has the name/description/product_image.file_name fields the account page's template renders")]
    public async Task List_FavoriteItem_HasProductFieldsTheTemplateRenders()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Pact Probe List Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(token, CreatedProductId);
        CreatedFavoriteOwnerToken = token;

        var response = await ApiContext.GetAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        var favorite = body.EnumerateArray().First(f => f.GetProperty("id").GetString() == CreatedFavoriteId);
        var product = favorite.GetProperty("product");

        // favorites.component.html: {{ favorite.product.name }}, {{ favorite.product.description | truncate }}
        Assert.That(product.GetProperty("name").GetString(), Is.Not.Null.And.Not.Empty);
        Assert.That(product.GetProperty("description").GetString(), Is.Not.Null);
        // favorites.component.html: assets/img/products/{{favorite.product.product_image.file_name}}
        Assert.That(product.GetProperty("product_image").GetProperty("file_name").GetString(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    [AllureName("POST /favorites duplicate-product conflict's message matches the exact string detail.component.ts's error handler checks for")]
    public async Task Create_DuplicateFavorite_MatchesConsumersErrorStringCheck()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Pact Probe Duplicate Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(token, CreatedProductId);
        CreatedFavoriteOwnerToken = token;

        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new { product_id = CreatedProductId },
        });

        Assert.That(response.Status, Is.EqualTo(409));
        var body = (await response.JsonAsync())!.Value;
        // detail.component.ts: if (response.error.message === 'Duplicate Entry') -> toasts
        // 'toasts.product-already-in-favorites'. A looser Does.Contain would miss a regression
        // that silently breaks this exact-match branch while still "sounding" similar.
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Duplicate Entry"));
    }

    [Test]
    [AllureName("POST /favorites with no auth's message matches the exact string detail.component.ts's error handler checks for")]
    public async Task Create_NoAuth_MatchesConsumersErrorStringCheck()
    {
        CreatedProductId = await CreateThrowawayProductAsync("Pact Probe NoAuth Product");

        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            DataObject = new { product_id = CreatedProductId },
        });

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        // detail.component.ts: else if (response.error.message === 'Unauthorized') -> toasts
        // 'toasts.unauthorized-favorite'.
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));
    }

    [Test]
    [AllureName("After DELETE /favorites/{id}, a subsequent GET /favorites (the list the UI actually reloads from) no longer includes it")]
    public async Task Delete_ThenReloadingTheList_NoLongerIncludesIt()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Pact Probe Delete Reload Product");
        var favoriteId = await CreateThrowawayFavoriteAsync(token, CreatedProductId);

        var deleteResponse = await DeleteFavoriteAsync(favoriteId, token);
        Assert.That(deleteResponse.Status, Is.EqualTo(204));

        // favorites.component.ts's deleteFavorite(): on success, calls loadFavorites() -> GET /favorites.
        var listResponse = await ApiContext.GetAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });
        var body = (await listResponse.JsonAsync())!.Value;
        Assert.That(body.EnumerateArray().Any(f => f.GetProperty("id").GetString() == favoriteId), Is.False);
    }
}
