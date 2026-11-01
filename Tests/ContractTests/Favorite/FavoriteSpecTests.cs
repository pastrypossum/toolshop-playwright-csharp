using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Favorite;

/// <summary>
/// Contract tests for every <c>/favorites*</c> operation against its documented OpenAPI schema
/// (<c>{API_URL}/docs</c>) - the shape, not the business data. One file per resource, grouped by
/// operation below (list / get-by-id / create / delete), per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Spec-track convention.
///
/// Unlike Category/Brand, every operation on this resource (including the writes) sits behind
/// <c>auth:users</c> - there's no no-auth-succeeds gap to document here. The two real gaps found
/// while probing the live endpoint are tracked separately, red, in <see cref="FavoriteGapSpecTests"/>.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Favorite")]
[AllureSubSuite("FavoriteSpecTests")]
public class FavoriteSpecTests : FavoriteContractTestBase
{
    // ---- GET /favorites ----

    [Test]
    [AllureName("GET /favorites with no auth returns 401 Unauthorized")]
    public async Task Get_NoAuth_ReturnsUnauthorized()
    {
        var response = await ApiContext.GetAsync("/favorites");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));
    }

    [Test]
    [AllureName("GET /favorites returns 200 with a JSON array, each item matching FavoriteWithProductResponse")]
    public async Task Get_WithAuth_ReturnsOkWithFavoriteWithProductShape()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Probe Favorite Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(token, CreatedProductId);
        CreatedFavoriteOwnerToken = token;

        var response = await ApiContext.GetAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["content-type"], Does.Contain("application/json"));

        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));

        var favorite = body.EnumerateArray().First(f => f.GetProperty("id").GetString() == CreatedFavoriteId);
        Assert.That(favorite.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "user_id", "product_id", "product" }));
        Assert.That(favorite.GetProperty("product_id").GetString(), Is.EqualTo(CreatedProductId));
        Assert.That(favorite.GetProperty("product").GetProperty("id").GetString(), Is.EqualTo(CreatedProductId));
    }

    [Test]
    [AllureName("GET /favorites only returns the authenticated user's own favorites, not another user's")]
    public async Task Get_OnlyReturnsOwnFavorites()
    {
        var customerToken = await GetCustomerTokenAsync();
        var customer2Token = await GetCustomer2TokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Probe Isolation Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(customerToken, CreatedProductId);
        CreatedFavoriteOwnerToken = customerToken;

        var response = await ApiContext.GetAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {customer2Token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateArray().Any(f => f.GetProperty("id").GetString() == CreatedFavoriteId), Is.False,
            "customer2 should not see a favorite created by customer");
    }

    [Test]
    [AllureName("An unsupported method on /favorites returns 405 per the documented contract")]
    public async Task Put_UnsupportedMethod_ReturnsMethodNotAllowed()
    {
        // The collection route only documents GET and POST; PUT/PATCH/DELETE must 405 here per
        // the OpenAPI spec's MethodNotAllowedResponse.
        var response = await ApiContext.PutAsync("/favorites");

        Assert.That(response.Status, Is.EqualTo(405));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Method is not allowed for the requested route"));
    }

    // ---- GET /favorites/{id} ----

    [Test]
    [AllureName("GET /favorites/{id} with no auth returns 401 Unauthorized")]
    public async Task GetById_NoAuth_ReturnsUnauthorized()
    {
        var response = await ApiContext.GetAsync("/favorites/does-not-exist");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));
    }

    [Test]
    [AllureName("GET /favorites/{id} returns 200 with a FavoriteResponse - no nested product, unlike the list endpoint")]
    public async Task GetById_ExistingFavorite_ReturnsOkWithFlatShape()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Probe GetById Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(token, CreatedProductId);
        CreatedFavoriteOwnerToken = token;

        var response = await ApiContext.GetAsync($"/favorites/{CreatedFavoriteId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        // Verified live: unlike GET /favorites, the single-item route does NOT include the
        // nested "product" - matches the spec's distinct FavoriteResponse (vs FavoriteWithProductResponse).
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "user_id", "product_id" }));
        Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(CreatedFavoriteId));
        Assert.That(body.GetProperty("product_id").GetString(), Is.EqualTo(CreatedProductId));
    }

    [Test]
    [AllureName("GET /favorites/{id} for a missing favorite returns 404 with ItemNotFoundResponse")]
    public async Task GetById_MissingFavorite_ReturnsNotFoundWithMessage()
    {
        var token = await GetCustomerTokenAsync();

        var response = await ApiContext.GetAsync("/favorites/does-not-exist", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(404));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Requested item not found"));
    }

    // ---- POST /favorites ----

    [Test]
    [AllureName("POST /favorites with no auth returns 401 Unauthorized - unlike POST /categories and /brands, this write IS gated")]
    public async Task Create_NoAuth_ReturnsUnauthorized()
    {
        var productId = await CreateThrowawayProductAsync("Spec Probe NoAuth Product");
        CreatedProductId = productId;

        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            DataObject = new { product_id = productId },
        });

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));
    }

    [Test]
    [AllureName("POST /favorites with a valid product_id returns 201 with a FavoriteResponse")]
    public async Task Create_ValidProduct_ReturnsCreated()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Probe Create Product");

        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new { product_id = CreatedProductId },
        });

        Assert.That(response.Status, Is.EqualTo(201));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "user_id", "product_id" }));
        Assert.That(body.GetProperty("product_id").GetString(), Is.EqualTo(CreatedProductId));

        CreatedFavoriteId = body.GetProperty("id").GetString();
        CreatedFavoriteOwnerToken = token;
    }

    [Test]
    [AllureName("POST /favorites with a missing product_id returns 422 with a field-keyed message")]
    public async Task Create_MissingProductId_ReturnsUnprocessableEntity()
    {
        var token = await GetCustomerTokenAsync();

        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("product_id")[0].GetString(), Is.EqualTo("The product id field is required."));
    }

    [Test]
    [AllureName("POST /favorites with a product_id that doesn't exist returns 422 with a field-keyed message")]
    public async Task Create_NonExistentProductId_ReturnsUnprocessableEntity()
    {
        var token = await GetCustomerTokenAsync();

        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new { product_id = "does-not-exist" },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("product_id")[0].GetString(), Is.EqualTo("The selected product id is invalid."));
    }

    [Test]
    [AllureName("POST /favorites twice for the same user+product returns 409 {message: 'Duplicate Entry'} - the DB's unique constraint, not a validated conflict")]
    public async Task Create_DuplicateFavorite_ReturnsConflict()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Probe Duplicate Product");
        CreatedFavoriteId = await CreateThrowawayFavoriteAsync(token, CreatedProductId);
        CreatedFavoriteOwnerToken = token;

        var response = await ApiContext.PostAsync("/favorites", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new { product_id = CreatedProductId },
        });

        Assert.That(response.Status, Is.EqualTo(409));
        var body = (await response.JsonAsync())!.Value;
        // No `unique` validation rule exists on StoreFavorite (only `exists:products,id`), so this
        // isn't a field-keyed MessageBag like Category/Brand's slug conflict - it's the generic
        // shape the global Handler produces from the DB's unique("user_id","product_id") constraint
        // (QueryException error code 1062). The frontend's error handler checks this exact string
        // (detail.component.ts's addToFavorites) - verified live, matches.
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Duplicate Entry"));
    }

    // ---- DELETE /favorites/{id} ----

    [Test]
    [AllureName("DELETE /favorites/{id} with no auth returns 401 Unauthorized")]
    public async Task Delete_NoAuth_ReturnsUnauthorized()
    {
        var response = await ApiContext.DeleteAsync("/favorites/does-not-exist");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));
    }

    [Test]
    [AllureName("DELETE /favorites/{id} with the owner's auth returns 204 and the favorite is actually gone")]
    public async Task Delete_WithAuth_DeletesFavorite()
    {
        var token = await GetCustomerTokenAsync();
        CreatedProductId = await CreateThrowawayProductAsync("Spec Probe Delete Product");
        var favoriteId = await CreateThrowawayFavoriteAsync(token, CreatedProductId);

        var response = await DeleteFavoriteAsync(favoriteId, token);
        Assert.That(response.Status, Is.EqualTo(204));

        var reloaded = await ApiContext.GetAsync($"/favorites/{favoriteId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });
        Assert.That(reloaded.Status, Is.EqualTo(404));
    }
}
