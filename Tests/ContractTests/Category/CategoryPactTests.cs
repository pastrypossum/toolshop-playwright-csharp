using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Category;

/// <summary>
/// Contract tests built from real consumer dependencies rather than the OpenAPI spec alone - the
/// layer on top of <see cref="CategorySpecTests"/>'s specification-based checks. One file per
/// resource, covering every consumer dependency found for it, per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Pact-track convention.
///
/// Unlike <c>Tests/ContractTests/Brand/BrandPactTests.cs</c>, these dependencies were traced by
/// reading the Angular frontend source directly (available alongside this repo at
/// <c>practice-software-testing/sprint5/UI/src/app</c>) rather than recording with
/// <c>playwright-cli</c> - no fixture file, the request/response shapes asserted below were
/// verified live against the deployed environment.
///
/// <see cref="CategoryId_FromTreeResponse_FiltersProductsByCategoryCorrectly"/>: traced from
/// <c>products/category/category.component.ts</c>'s <c>filterByCategory()</c>, which takes a
/// category <c>id</c> out of the tree returned by
/// <c>CategoryService.getSubCategoriesTreeBySlug()</c> (<c>QUERY /categories/tree</c>) and later
/// sends it straight back as <c>ProductService.getProductsByCategoryAndBrand()</c>'s
/// <c>by_category</c> filter (see <c>_services/product.service.ts</c>, line 65:
/// <c>criteria['by_category'] = categoryIds</c>) - the one thing the <c>/categories</c> spec
/// alone cannot express.
///
/// <see cref="Create_ValidationFailure_ReturnsFieldKeyedErrors"/> and
/// <see cref="Create_SlugConflict_ReturnsFieldKeyedErrors"/>: built from tracing
/// <c>admin/categories-add-edit/categories-add-edit.component.ts</c>'s
/// <c>createBrand()</c>/<c>updateBrand()</c> error callbacks (method names are a leftover from
/// copy-pasting the brand form - they operate on categories), which do
/// <c>Object.values(err).map(v => Array.isArray(v) ? v.join(...) : v)</c>, the same pattern as
/// Brand's admin form. This only renders something useful when the error body is a field-keyed
/// MessageBag (<c>{"name": ["..."], "slug": ["..."]}</c>), not a single
/// <c>{"message": "..."}</c> string. These assert the provider still returns that shape for
/// create validation and conflict failures.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Category")]
[AllureSubSuite("CategoryPactTests")]
public class CategoryPactTests : ApiTest
{
    [Test]
    [AllureName("A category id from GET /categories/tree filters GET /products as the real consumer relies on")]
    public async Task CategoryId_FromTreeResponse_FiltersProductsByCategoryCorrectly()
    {
        var treeResponse = await ApiContext.GetAsync("/categories/tree?by_category_slug=hand-tools");
        var subTree = (await treeResponse.JsonAsync())!.Value[0];
        var subCategories = subTree.GetProperty("sub_categories");

        Assert.That(subCategories.GetArrayLength(), Is.GreaterThan(0), "expected 'hand-tools' to have seeded sub-categories to filter by");

        foreach (var subCategory in subCategories.EnumerateArray())
        {
            var categoryId = subCategory.GetProperty("id").GetString();
            var categoryName = subCategory.GetProperty("name").GetString();

            var productsResponse = await ApiContext.GetAsync($"/products?by_category={categoryId}");
            Assert.That(productsResponse.Status, Is.EqualTo(200));

            var products = (await productsResponse.JsonAsync())!.Value.GetProperty("data");
            Assert.That(products.GetArrayLength(), Is.GreaterThan(0),
                $"expected at least one product for category '{categoryName}'");

            foreach (var product in products.EnumerateArray())
            {
                Assert.That(product.GetProperty("category").GetProperty("id").GetString(), Is.EqualTo(categoryId),
                    $"product returned for by_category={categoryId} ('{categoryName}') belongs to a different category");
            }
        }
    }

    [Test]
    [AllureName("POST /categories validation failure returns a field-keyed MessageBag, as the admin UI's error renderer expects")]
    public async Task Create_ValidationFailure_ReturnsFieldKeyedErrors()
    {
        var response = await ApiContext.PostAsync("/categories", new APIRequestContextOptions
        {
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;

        // Object.values(err) in the consumer needs every top-level value to be a string[]
        // (or a plain string) - not a nested object, not a bare message. Assert that shape.
        foreach (var property in body.EnumerateObject())
        {
            Assert.That(property.Value.ValueKind, Is.EqualTo(JsonValueKind.Array),
                $"'{property.Name}' must be an array for Object.values(err).join() to render it correctly");
        }
    }

    [Test]
    [AllureName("POST /categories slug conflict returns a field-keyed MessageBag, as the admin UI's error renderer expects")]
    public async Task Create_SlugConflict_ReturnsFieldKeyedErrors()
    {
        var response = await ApiContext.PostAsync("/categories", new APIRequestContextOptions
        {
            DataObject = new { name = "Duplicate Probe", slug = "hand-tools" },
        });

        Assert.That(response.Status, Is.EqualTo(409));
        var body = (await response.JsonAsync())!.Value;

        foreach (var property in body.EnumerateObject())
        {
            Assert.That(property.Value.ValueKind, Is.EqualTo(JsonValueKind.Array),
                $"'{property.Name}' must be an array for Object.values(err).join() to render it correctly");
        }
    }
}
