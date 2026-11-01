using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Category;

/// <summary>
/// Contract tests for every <c>/categories*</c> operation against its documented OpenAPI schema
/// (<c>{API_URL}/docs</c>) - the shape, not the business data. One file per resource, grouped by
/// operation below (list / tree / get-by-id / search / create / update / delete), per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Spec-track convention.
///
/// The category resource documents two distinct read shapes:
/// <c>CategoryResponse</c> (flat, used by the plain list/search) and <c>CategoryTreeResponse</c>
/// (adds <c>sub_categories</c>, used by the <c>/tree</c> endpoints) - both schemas are
/// structurally identical in <c>/docs</c>, but what the live endpoints actually return differs:
/// see the per-test comments below for where reality and the spec disagree.
///
/// Known gaps tracked separately and deliberately red: see <see cref="CategoryAuthGapSpecTests"/>.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Category")]
[AllureSubSuite("CategorySpecTests")]
public class CategorySpecTests : CategoryContractTestBase
{
    private static readonly string[] FlatCategoryProperties = ["id", "name", "slug", "parent_id"];
    private static readonly string[] TreeCategoryProperties = ["id", "name", "slug", "parent_id", "sub_categories"];

    // ---- GET /categories ----

    [Test]
    [AllureName("GET /categories returns 200 with a JSON array")]
    public async Task Get_ReturnsOkWithJsonArray()
    {
        var response = await ApiContext.GetAsync("/categories");

        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["content-type"], Does.Contain("application/json"));

        var body = await response.JsonAsync();
        Assert.That(body!.Value.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    [AllureName("Every category in the flat list has exactly id/name/slug/parent_id - the spec's documented sub_categories is not actually returned here")]
    public async Task Get_EveryCategoryMatchesFlatShape()
    {
        var response = await ApiContext.GetAsync("/categories");
        var categories = await response.JsonAsync();

        Assert.That(categories!.Value.GetArrayLength(), Is.GreaterThan(0), "expected at least one seeded category");

        foreach (var category in categories.Value.EnumerateArray())
        {
            // /docs declares this endpoint's items as CategoryResponse, which includes
            // sub_categories - the live endpoint omits it entirely (not even an empty array),
            // unlike /categories/tree and /categories/search which both include it. Asserting
            // the real shape here, not the documented one - found by probing the live endpoint.
            Assert.That(category.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(FlatCategoryProperties));

            Assert.That(category.GetProperty("id").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(category.GetProperty("name").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(category.GetProperty("slug").GetString(), Is.Not.Null.And.Not.Empty);
            // parent_id is nullable - top-level categories have no parent.
            var parentId = category.GetProperty("parent_id");
            Assert.That(parentId.ValueKind, Is.EqualTo(JsonValueKind.String).Or.EqualTo(JsonValueKind.Null));
        }
    }

    [Test]
    [AllureName("An unsupported method on /categories returns 405 per the documented contract")]
    public async Task Put_UnsupportedMethod_ReturnsMethodNotAllowed()
    {
        // The collection route only documents GET and POST; PUT/PATCH/DELETE belong to
        // /categories/{categoryId} and must 405 here per the OpenAPI spec's MethodNotAllowedResponse.
        var response = await ApiContext.PutAsync("/categories");

        Assert.That(response.Status, Is.EqualTo(405));
    }

    [Test]
    [AllureName("GET /categories/{id} returns 405 - there is no such route, single-category reads go through /categories/tree/{id}")]
    public async Task GetById_ReturnsMethodNotAllowed()
    {
        // Unlike /brands/{brandId}, /categories/{categoryId} has no GET operation in the spec at
        // all (only PUT/PATCH/DELETE) - GET-by-id is served exclusively by /categories/tree/{id}.
        var response = await ApiContext.GetAsync("/categories/does-not-exist");

        Assert.That(response.Status, Is.EqualTo(405));
    }

    // ---- GET /categories/tree, QUERY /categories/tree ----

    [Test]
    [AllureName("GET /categories/tree returns every top-level category nested with its sub_categories")]
    public async Task Tree_ReturnsOkWithNestedCategories()
    {
        var response = await ApiContext.GetAsync("/categories/tree");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0));

        foreach (var category in body.EnumerateArray())
        {
            Assert.That(category.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(TreeCategoryProperties));
            Assert.That(category.GetProperty("sub_categories").ValueKind, Is.EqualTo(JsonValueKind.Array));
        }
    }

    [Test]
    [AllureName("GET /categories/tree?by_category_slug filters to that category's own subtree")]
    public async Task Tree_FilteredBySlug_ReturnsOnlyThatSubtree()
    {
        var response = await ApiContext.GetAsync("/categories/tree?by_category_slug=hand-tools");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        Assert.That(body[0].GetProperty("slug").GetString(), Is.EqualTo("hand-tools"));
        Assert.That(body[0].GetProperty("sub_categories").GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    [AllureName("GET /categories/tree/{id} returns 200 with a CategoryTreeResponse for an existing category")]
    public async Task TreeById_ExistingCategory_ReturnsOkWithTreeShape()
    {
        CreatedCategoryId = await CreateThrowawayCategoryAsync("Spec Probe Category");

        var response = await ApiContext.GetAsync($"/categories/tree/{CreatedCategoryId}");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(TreeCategoryProperties));
        Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(CreatedCategoryId));
        Assert.That(body.GetProperty("sub_categories").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    [AllureName("GET /categories/tree/{id} returns 404 with ItemNotFoundResponse for a missing category")]
    public async Task TreeById_MissingCategory_ReturnsNotFoundWithMessage()
    {
        var response = await ApiContext.GetAsync("/categories/tree/does-not-exist");

        Assert.That(response.Status, Is.EqualTo(404));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Requested item not found"));
    }

    [Test]
    [AllureName("QUERY /categories/tree (RFC 9110 QUERY) matches the GET variant's response shape")]
    public async Task Tree_QueryMethodVariant_MatchesGetVariant()
    {
        var response = await ApiContext.FetchAsync("/categories/tree", new APIRequestContextOptions
        {
            Method = "QUERY",
            DataObject = new { by_category_slug = "hand-tools" },
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(1));
        Assert.That(body[0].GetProperty("slug").GetString(), Is.EqualTo("hand-tools"));
    }

    // ---- GET /categories/search, QUERY /categories/search ----

    [Test]
    [AllureName("GET /categories/search returns matching categories, each with sub_categories like the tree endpoints")]
    public async Task Search_MatchingQuery_ReturnsArrayOfCategories()
    {
        var response = await ApiContext.GetAsync("/categories/search?q=Hammer");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0));
        foreach (var category in body.EnumerateArray())
        {
            // Unlike the plain /categories list, /search does include sub_categories (here always
            // an empty array for this seeded data) - the same shape as /categories/tree.
            Assert.That(category.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(TreeCategoryProperties));
        }
    }

    [Test]
    [AllureName("GET /categories/search with no matches returns an empty array, not 404")]
    public async Task Search_NoMatches_ReturnsEmptyArray()
    {
        var response = await ApiContext.GetAsync("/categories/search?q=zzz-no-such-category-zzz");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    [AllureName("QUERY /categories/search (RFC 9110 QUERY) matches the GET variant's response shape")]
    public async Task Search_QueryMethodVariant_MatchesGetVariant()
    {
        var response = await ApiContext.FetchAsync("/categories/search", new APIRequestContextOptions
        {
            Method = "QUERY",
            DataObject = new { q = "Hammer" },
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetArrayLength(), Is.GreaterThan(0));
    }

    // ---- POST /categories ----

    [Test]
    [AllureName("POST /categories with no auth still succeeds with 201 - spec/implementation gap, asserted on purpose")]
    public async Task Create_ValidCategory_NoAuth_ReturnsCreated()
    {
        var response = await ApiContext.PostAsync("/categories", new APIRequestContextOptions
        {
            DataObject = new { name = "Spec Probe Category", slug = $"spec-probe-category-{Guid.NewGuid():N}" },
        });

        Assert.That(response.Status, Is.EqualTo(201));
        var body = (await response.JsonAsync())!.Value;
        // A top-level category created without a parent_id omits the key entirely in the
        // response, rather than returning it as null - verified live, not documented in /docs.
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "name", "slug" }));
        CreatedCategoryId = body.GetProperty("id").GetString();
    }

    [Test]
    [AllureName("POST /categories with a parent_id echoes it back in the response")]
    public async Task Create_WithParentId_ReturnsCreatedWithParentId()
    {
        var parentId = await CreateThrowawayCategoryAsync("Spec Probe Parent");
        var token = await GetAdminTokenAsync();

        var response = await ApiContext.PostAsync("/categories", new APIRequestContextOptions
        {
            DataObject = new { name = "Spec Probe Child", slug = $"spec-probe-child-{Guid.NewGuid():N}", parent_id = parentId },
        });

        Assert.That(response.Status, Is.EqualTo(201));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("parent_id").GetString(), Is.EqualTo(parentId));
        var childId = body.GetProperty("id").GetString()!;

        await DeleteCategoryAsync(childId, token);
        CreatedCategoryId = parentId;
    }

    [Test]
    [AllureName("POST /categories with a missing required field returns 422 with a field-keyed message")]
    public async Task Create_MissingRequiredFields_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.PostAsync("/categories", new APIRequestContextOptions
        {
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        // The spec's UnprocessableEntityResponse component has no schema at all - this is the
        // real shape, found by probing the live endpoint, not documented anywhere in /docs.
        Assert.That(body.GetProperty("name")[0].GetString(), Is.EqualTo("The name field is required."));
        Assert.That(body.GetProperty("slug")[0].GetString(), Is.EqualTo("The slug field is required."));
    }

    [Test]
    [AllureName("POST /categories with a slug that already exists returns 409 with a field-keyed message")]
    public async Task Create_DuplicateSlug_ReturnsConflict()
    {
        var response = await ApiContext.PostAsync("/categories", new APIRequestContextOptions
        {
            DataObject = new { name = "Duplicate Probe", slug = "hand-tools" }, // seeded category's real slug
        });

        Assert.That(response.Status, Is.EqualTo(409));
        var body = (await response.JsonAsync())!.Value;
        // DuplicateConflictResponse's first oneOf branch - field-level MessageBag.
        Assert.That(body.GetProperty("slug")[0].GetString(), Does.Contain("already exists"));
    }

    // ---- PUT /categories/{id}, PATCH /categories/{id} ----

    [Test]
    [AllureName("PUT /categories/{id} with no auth still returns 200 {success:true} - spec/implementation gap, asserted on purpose")]
    public async Task Put_ValidCategory_NoAuth_ReturnsSuccess()
    {
        CreatedCategoryId = await CreateThrowawayCategoryAsync("Update Probe Category");

        var response = await ApiContext.PutAsync($"/categories/{CreatedCategoryId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Updated Name", slug = $"update-probe-{Guid.NewGuid():N}" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);
    }

    [Test]
    [AllureName("PATCH /categories/{id} with a single field returns 200 {success:true}, and only touches that field")]
    public async Task Patch_SingleField_OnlyChangesThatField()
    {
        CreatedCategoryId = await CreateThrowawayCategoryAsync("Update Probe Category");
        var before = await ApiContext.GetAsync($"/categories/tree/{CreatedCategoryId}");
        var slugBeforePatch = (await before.JsonAsync())!.Value.GetProperty("slug").GetString();

        var response = await ApiContext.PatchAsync($"/categories/{CreatedCategoryId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Patched Name" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);

        var afterBody = (await (await ApiContext.GetAsync($"/categories/tree/{CreatedCategoryId}")).JsonAsync())!.Value;
        Assert.That(afterBody.GetProperty("name").GetString(), Is.EqualTo("Patched Name"), "the field that was patched should change");
        // The actual distinction between PATCH and PUT: a field not sent must be left alone.
        Assert.That(afterBody.GetProperty("slug").GetString(), Is.EqualTo(slugBeforePatch), "a field not sent in a PATCH must not change");
    }

    [Test]
    [AllureName("PUT /categories/{id} for a missing category returns 404 with a message")]
    public async Task Put_MissingCategory_ReturnsNotFound()
    {
        var response = await ApiContext.PutAsync("/categories/does-not-exist", new APIRequestContextOptions
        {
            DataObject = new { name = "Doesn't matter", slug = "doesnt-matter" },
        });

        Assert.That(response.Status, Is.EqualTo(404));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Requested item not found"));
    }

    // ---- DELETE /categories/{id} ----

    [Test]
    [AllureName("DELETE /categories/{id} with no auth returns 401 Unauthorized")]
    public async Task Delete_NoAuth_ReturnsUnauthorized()
    {
        var categoryId = await CreateThrowawayCategoryAsync("Delete Probe Category");

        var response = await ApiContext.DeleteAsync($"/categories/{categoryId}");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));

        // Clean up properly now that we've proven the unauthenticated attempt didn't delete it.
        var token = await GetAdminTokenAsync();
        await DeleteCategoryAsync(categoryId, token);
    }

    [Test]
    [AllureName("DELETE /categories/{id} with admin auth returns 204 and the category is actually gone")]
    public async Task Delete_WithAuth_DeletesCategory()
    {
        var categoryId = await CreateThrowawayCategoryAsync("Delete Probe Category");
        var token = await GetAdminTokenAsync();

        var response = await DeleteCategoryAsync(categoryId, token);
        Assert.That(response.Status, Is.EqualTo(204));

        var reloaded = await ApiContext.GetAsync($"/categories/tree/{categoryId}");
        Assert.That(reloaded.Status, Is.EqualTo(404));
    }

    [Test]
    [AllureName("DELETE /categories/{id} for a missing category returns 404, even with valid auth")]
    public async Task Delete_MissingCategory_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();

        var response = await DeleteCategoryAsync("does-not-exist", token);

        Assert.That(response.Status, Is.EqualTo(404));
    }

    [Test]
    [AllureName("DELETE /categories/{id} for a category with a sub-category returns 409, not the spec's undocumented generic shape")]
    public async Task Delete_CategoryWithSubCategory_ReturnsConflict()
    {
        var token = await GetAdminTokenAsync();
        var parentId = await CreateThrowawayCategoryAsync("Delete Conflict Probe Parent", bearerToken: token);
        var childId = await CreateThrowawayCategoryAsync("Delete Conflict Probe Child", parentId, token);

        try
        {
            var response = await DeleteCategoryAsync(parentId, token);

            Assert.That(response.Status, Is.EqualTo(409));
            var body = (await response.JsonAsync())!.Value;
            // The spec's ConflictResponse component has no schema at all - this real shape
            // ({"success":false,"message":"..."}) was found by probing the live endpoint, not
            // documented anywhere in /docs, and matches what Brand's equivalent in-use conflict
            // returns (see BrandSpecTests.Delete_BrandInUse_ReturnsConflict).
            Assert.That(body.GetProperty("success").GetBoolean(), Is.False);
            Assert.That(body.GetProperty("message").GetString(), Does.Contain("used elsewhere"));
        }
        finally
        {
            await DeleteCategoryAsync(childId, token);
            await DeleteCategoryAsync(parentId, token);
        }
    }

    [Test]
    [AllureName("DELETE /categories/{id} for a category used by a product returns 409, not the spec's undocumented generic shape")]
    public async Task Delete_CategoryInUse_ReturnsConflict()
    {
        var token = await GetAdminTokenAsync();
        var categoryId = await CreateThrowawayCategoryAsync("Delete Conflict Probe Category", bearerToken: token);

        var brandId = (await (await ApiContext.GetAsync("/brands")).JsonAsync())!.Value[0].GetProperty("id").GetString();
        var sampleProduct = (await (await ApiContext.GetAsync("/products")).JsonAsync())!.Value.GetProperty("data")[0];
        var imageId = sampleProduct.GetProperty("product_image").GetProperty("id").GetString();

        var productResponse = await ApiContext.PostAsync("/products", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            DataObject = new
            {
                name = "Delete Conflict Probe Product",
                description = "created to make the category it references in-use",
                price = 9.99,
                category_id = categoryId,
                brand_id = brandId,
                product_image_id = imageId,
                is_location_offer = false,
                is_rental = false,
            },
        });
        Assert.That(productResponse.Status, Is.EqualTo(201), "setup failed: could not create the dependent product");
        var productId = (await productResponse.JsonAsync())!.Value.GetProperty("id").GetString();

        try
        {
            var response = await DeleteCategoryAsync(categoryId, token);

            Assert.That(response.Status, Is.EqualTo(409));
            var body = (await response.JsonAsync())!.Value;
            Assert.That(body.GetProperty("success").GetBoolean(), Is.False);
            Assert.That(body.GetProperty("message").GetString(), Does.Contain("used elsewhere"));
        }
        finally
        {
            await ApiContext.DeleteAsync($"/products/{productId}", new APIRequestContextOptions
            {
                Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
            });
            await DeleteCategoryAsync(categoryId, token);
        }
    }
}
