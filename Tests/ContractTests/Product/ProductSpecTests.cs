using System.Text.Json;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.Product;

/// <summary>
/// Contract tests for every <c>/products*</c> operation against its documented OpenAPI schema
/// (<c>{API_URL}/docs</c>) - the shape, not the business data. One file per resource, grouped by
/// operation below (list / get-by-id / related / search / create / update / delete), per
/// <c>.claude/skills/contract-testing/SKILL.md</c>'s Spec-track convention.
///
/// <c>ProductResponse</c> documents 12 properties (id/name/description/price/is_location_offer/
/// is_rental/in_stock/co2_rating/is_eco_friendly/brand/category/product_image). What <c>GET
/// /products/{id}</c> actually returns has a 13th, undocumented property: <c>specs</c> - the
/// product's ProductSpec sibling resource, eager-loaded by <c>ProductService::getProductById()</c>
/// and not in <c>Product::$hidden</c>, so it serializes straight through. See
/// <see cref="GetById_ExistingProduct_IncludesUndocumentedSpecsArray"/> -
/// this is the one place the Product/ProductSpec relationship surfaces on the wire for this
/// resource's own spec track; the fuller cross-resource behaviour (a spec created under a
/// product immediately reflected back here, the <c>by_spec</c> filter, cascade delete) lives in
/// <c>Tests/ContractTests/ProductSpec/ProductSpecPactTests.cs</c>, traced from the real Angular
/// consumers rather than the spec alone.
///
/// Known gaps tracked separately and deliberately red: see <see cref="ProductAuthGapSpecTests"/>.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Product")]
[AllureSubSuite("ProductSpecTests")]
public class ProductSpecTests : ProductContractTestBase
{
    private static readonly string[] ProductResponseProperties =
    [
        "id", "name", "description", "price", "is_location_offer", "is_rental",
        "in_stock", "co2_rating", "is_eco_friendly", "brand", "category", "product_image",
    ];

    private static readonly string[] PaginationEnvelopeProperties =
        ["current_page", "data", "from", "last_page", "per_page", "to", "total"];

    // ---- GET /products ----

    [Test]
    [AllureName("GET /products returns 200 with a PaginatedProductResponse envelope")]
    public async Task Get_ReturnsOkWithPaginationEnvelope()
    {
        var response = await ApiContext.GetAsync("/products");

        Assert.That(response.Status, Is.EqualTo(200));
        Assert.That(response.Headers["content-type"], Does.Contain("application/json"));

        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(PaginationEnvelopeProperties));
        Assert.That(body.GetProperty("data").ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(body.GetProperty("data").GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    [AllureName("Every product in the list matches the documented ProductResponse shape exactly")]
    public async Task Get_EveryProductMatchesResponseShape()
    {
        var response = await ApiContext.GetAsync("/products");
        var products = (await response.JsonAsync())!.Value.GetProperty("data");

        foreach (var product in products.EnumerateArray())
        {
            // Unlike GET /products/{id} (see ProductSpecTests in the sibling ProductSpec
            // namespace... actually see GetById below), the list does NOT eager-load specs -
            // ProductService::getAllProducts() never touches the specs relation.
            Assert.That(product.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(ProductResponseProperties));
        }
    }

    [Test]
    [AllureName("GET /products with no is_rental param defaults to non-rental products only - undocumented default filter")]
    public async Task Get_DefaultsToExcludingRentals()
    {
        // ProductService::getAllProducts() forces filters['is_rental'] = 0 whenever the
        // 'is_rental' query param is absent - not documented in /docs, found by probing the
        // live endpoint and confirmed by reading ProductService.php directly.
        var response = await ApiContext.GetAsync("/products");
        var products = (await response.JsonAsync())!.Value.GetProperty("data");

        Assert.That(products.GetArrayLength(), Is.GreaterThan(0));
        foreach (var product in products.EnumerateArray())
        {
            Assert.That(product.GetProperty("is_rental").GetBoolean(), Is.False);
        }
    }

    [Test]
    [AllureName("GET /products?is_rental=true returns only rental products")]
    public async Task Get_IsRentalTrue_ReturnsOnlyRentals()
    {
        var response = await ApiContext.GetAsync("/products?is_rental=true");

        Assert.That(response.Status, Is.EqualTo(200));
        var products = (await response.JsonAsync())!.Value.GetProperty("data");
        Assert.That(products.GetArrayLength(), Is.GreaterThan(0), "expected at least one seeded rental product");
        foreach (var product in products.EnumerateArray())
        {
            Assert.That(product.GetProperty("is_rental").GetBoolean(), Is.True);
        }
    }

    [Test]
    [AllureName("An unsupported method on /products returns 405 per the documented contract")]
    public async Task Put_UnsupportedMethod_ReturnsMethodNotAllowed()
    {
        var response = await ApiContext.PutAsync("/products");

        Assert.That(response.Status, Is.EqualTo(405));
    }

    [Test]
    [AllureName("QUERY /products (RFC 9110 QUERY) matches the GET variant's response shape")]
    public async Task Query_QueryMethodVariant_MatchesGetVariant()
    {
        var response = await ApiContext.FetchAsync("/products", new APIRequestContextOptions
        {
            Method = "QUERY",
            DataObject = new { is_rental = "true" },
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(PaginationEnvelopeProperties));
        var products = body.GetProperty("data");
        Assert.That(products.GetArrayLength(), Is.GreaterThan(0));
        foreach (var product in products.EnumerateArray())
        {
            Assert.That(product.GetProperty("is_rental").GetBoolean(), Is.True);
        }
    }

    // ---- GET /products/{id} ----

    [Test]
    [AllureName("GET /products/{id} returns 200 with the documented shape plus an undocumented specs array")]
    public async Task GetById_ExistingProduct_IncludesUndocumentedSpecsArray()
    {
        var response = await ApiContext.GetAsync("/products");
        var sampleProductId = (await response.JsonAsync())!.Value.GetProperty("data")[0].GetProperty("id").GetString();

        var showResponse = await ApiContext.GetAsync($"/products/{sampleProductId}");

        Assert.That(showResponse.Status, Is.EqualTo(200));
        var body = (await showResponse.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name),
            Is.EquivalentTo(ProductResponseProperties.Append("specs")),
            "GET /products/{id} should return the documented ProductResponse properties plus the undocumented 'specs' array");

        var specs = body.GetProperty("specs");
        Assert.That(specs.ValueKind, Is.EqualTo(JsonValueKind.Array));
        foreach (var spec in specs.EnumerateArray())
        {
            Assert.That(spec.EnumerateObject().Select(p => p.Name),
                Is.EquivalentTo(new[] { "id", "product_id", "spec_name", "spec_value", "spec_unit" }));
            Assert.That(spec.GetProperty("product_id").GetString(), Is.EqualTo(sampleProductId));
        }
    }

    [Test]
    [AllureName("GET /products/{id}'s nested category includes parent_id - the plain list's nested category does not")]
    public async Task GetById_NestedCategory_IncludesParentIdUnlikeListVariant()
    {
        // ProductService::getProductById() eager-loads 'category:id,name,slug,parent_id', while
        // getAllProducts()/withEagerLoading() only loads 'category:id,name,slug' - the same kind
        // of per-operation shape drift CategorySpecTests documents for its own resource.
        var response = await ApiContext.GetAsync("/products");
        var sampleProductId = (await response.JsonAsync())!.Value.GetProperty("data")[0].GetProperty("id").GetString();

        var showResponse = await ApiContext.GetAsync($"/products/{sampleProductId}");
        var category = (await showResponse.JsonAsync())!.Value.GetProperty("category");

        Assert.That(category.EnumerateObject().Select(p => p.Name),
            Is.EquivalentTo(new[] { "id", "name", "slug", "parent_id" }));
    }

    [Test]
    [AllureName("GET /products/{id} returns 404 with ItemNotFoundResponse for a missing product")]
    public async Task GetById_MissingProduct_ReturnsNotFoundWithMessage()
    {
        var response = await ApiContext.GetAsync("/products/does-not-exist");

        Assert.That(response.Status, Is.EqualTo(404));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Requested item not found"));
    }

    // ---- GET /products/{id}/related ----

    [Test]
    [AllureName("GET /products/{id}/related returns other products sharing the same category, excluding itself")]
    public async Task GetRelated_ReturnsSameCategoryProductsExcludingSelf()
    {
        var listResponse = await ApiContext.GetAsync("/products");
        var sampleProduct = (await listResponse.JsonAsync())!.Value.GetProperty("data")[0];
        var productId = sampleProduct.GetProperty("id").GetString();
        var categoryId = sampleProduct.GetProperty("category").GetProperty("id").GetString();

        var response = await ApiContext.GetAsync($"/products/{productId}/related");

        Assert.That(response.Status, Is.EqualTo(200));
        var related = (await response.JsonAsync())!.Value;
        Assert.That(related.ValueKind, Is.EqualTo(JsonValueKind.Array));
        foreach (var product in related.EnumerateArray())
        {
            Assert.That(product.GetProperty("id").GetString(), Is.Not.EqualTo(productId));
            Assert.That(product.GetProperty("category").GetProperty("id").GetString(), Is.EqualTo(categoryId));
        }
    }

    // ---- GET /products/search, QUERY /products/search ----

    [Test]
    [AllureName("GET /products/search returns matching products in a PaginatedProductResponse envelope")]
    public async Task Search_MatchingQuery_ReturnsPaginatedProducts()
    {
        var response = await ApiContext.GetAsync("/products/search?q=Hammer");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(PaginationEnvelopeProperties));
        Assert.That(body.GetProperty("data").GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    [AllureName("GET /products/search with no matches returns an empty data array, not 404")]
    public async Task Search_NoMatches_ReturnsEmptyDataArray()
    {
        var response = await ApiContext.GetAsync("/products/search?q=zzz-no-such-product-zzz");

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("data").GetArrayLength(), Is.EqualTo(0));
        Assert.That(body.GetProperty("total").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    [AllureName("QUERY /products/search (RFC 9110 QUERY) matches the GET variant's response shape")]
    public async Task Search_QueryMethodVariant_MatchesGetVariant()
    {
        var response = await ApiContext.FetchAsync("/products/search", new APIRequestContextOptions
        {
            Method = "QUERY",
            DataObject = new { q = "Hammer" },
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("data").GetArrayLength(), Is.GreaterThan(0));
    }

    // ---- POST /products ----

    [Test]
    [AllureName("POST /products with no auth still succeeds with 201 - spec/implementation gap, asserted on purpose")]
    public async Task Create_ValidProduct_NoAuth_ReturnsCreated()
    {
        var (categoryId, brandId, imageId) = await GetValidProductDependenciesAsync();

        var response = await ApiContext.PostAsync("/products", new APIRequestContextOptions
        {
            DataObject = new
            {
                name = "Spec Probe Product",
                description = "Created by ProductSpecTests",
                price = 9.99,
                category_id = categoryId,
                brand_id = brandId,
                product_image_id = imageId,
                is_location_offer = false,
                is_rental = false,
            },
        });

        Assert.That(response.Status, Is.EqualTo(201));
        var body = (await response.JsonAsync())!.Value;
        // The create response is its own shape again: no 'specs' key (createProduct() only
        // loads product_image/category/brand, never specs), and - unlike show - this id's
        // brand/category sub-objects aren't column-restricted the same way. Asserting only the
        // top-level fields that matter for identifying/cleaning up the created resource here;
        // the full ProductResponse shape is covered by GetById_ExistingProduct above.
        Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Spec Probe Product"));
        CreatedProductId = body.GetProperty("id").GetString();
    }

    [Test]
    [AllureName("POST /products with missing required fields returns 422 with field-keyed messages")]
    public async Task Create_MissingRequiredFields_ReturnsUnprocessableEntity()
    {
        var response = await ApiContext.PostAsync("/products", new APIRequestContextOptions
        {
            DataObject = new { },
        });

        Assert.That(response.Status, Is.EqualTo(422));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("name")[0].GetString(), Is.EqualTo("The name field is required."));
        Assert.That(body.GetProperty("price")[0].GetString(), Is.EqualTo("The price field is required."));
        Assert.That(body.GetProperty("category_id")[0].GetString(), Is.EqualTo("The category id field is required."));
        Assert.That(body.GetProperty("brand_id")[0].GetString(), Is.EqualTo("The brand id field is required."));
        Assert.That(body.GetProperty("product_image_id")[0].GetString(), Is.EqualTo("The product image id field is required."));
    }

    // ---- PUT /products/{id}, PATCH /products/{id} ----

    [Test]
    [AllureName("PUT /products/{id} with no auth still returns 200 {success:true} - spec/implementation gap, asserted on purpose")]
    public async Task Put_ValidProduct_NoAuth_ReturnsSuccess()
    {
        CreatedProductId = await CreateThrowawayProductAsync("Update Probe Product");

        var response = await ApiContext.PutAsync($"/products/{CreatedProductId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Updated Name" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);
    }

    [Test]
    [AllureName("PUT /products/{id} with a single field only changes that field - it behaves as a partial update, not a full replace")]
    public async Task Put_SingleField_OnlyChangesThatField()
    {
        CreatedProductId = await CreateThrowawayProductAsync("Update Probe Product");
        var before = (await (await ApiContext.GetAsync($"/products/{CreatedProductId}")).JsonAsync())!.Value;
        var priceBeforePut = before.GetProperty("price").GetDouble();

        var response = await ApiContext.PutAsync($"/products/{CreatedProductId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Put Patched Name" },
        });

        Assert.That(response.Status, Is.EqualTo(200));

        var after = (await (await ApiContext.GetAsync($"/products/{CreatedProductId}")).JsonAsync())!.Value;
        Assert.That(after.GetProperty("name").GetString(), Is.EqualTo("Put Patched Name"));
        // ProductController::update() passes $request->all() straight to Product::update() -
        // whatever wasn't sent is never touched, same as Brand/Category's own PUT. The
        // documented contract (ProductRequest as the full request body) implies a replace;
        // the live behaviour is a partial update.
        Assert.That(after.GetProperty("price").GetDouble(), Is.EqualTo(priceBeforePut), "a field not sent in PUT must not change");
    }

    [Test]
    [AllureName("PATCH /products/{id} with a single field returns 200 {success:true}, and only touches that field")]
    public async Task Patch_SingleField_OnlyChangesThatField()
    {
        CreatedProductId = await CreateThrowawayProductAsync("Update Probe Product");
        var before = (await (await ApiContext.GetAsync($"/products/{CreatedProductId}")).JsonAsync())!.Value;
        var priceBeforePatch = before.GetProperty("price").GetDouble();

        var response = await ApiContext.PatchAsync($"/products/{CreatedProductId}", new APIRequestContextOptions
        {
            DataObject = new { name = "Patched Name" },
        });

        Assert.That(response.Status, Is.EqualTo(200));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("success").GetBoolean(), Is.True);

        var after = (await (await ApiContext.GetAsync($"/products/{CreatedProductId}")).JsonAsync())!.Value;
        Assert.That(after.GetProperty("name").GetString(), Is.EqualTo("Patched Name"));
        Assert.That(after.GetProperty("price").GetDouble(), Is.EqualTo(priceBeforePatch), "a field not sent in a PATCH must not change");
    }

    [Test]
    [AllureName("PUT /products/{id} for a missing product returns 404 with a message")]
    public async Task Put_MissingProduct_ReturnsNotFound()
    {
        var response = await ApiContext.PutAsync("/products/does-not-exist", new APIRequestContextOptions
        {
            DataObject = new { name = "Doesn't matter" },
        });

        Assert.That(response.Status, Is.EqualTo(404));
    }

    // ---- DELETE /products/{id} ----

    [Test]
    [AllureName("DELETE /products/{id} with no auth returns 401 Unauthorized - genuinely enforced here, unlike POST/PUT/PATCH")]
    public async Task Delete_NoAuth_ReturnsUnauthorized()
    {
        var token = await GetAdminTokenAsync();
        var productId = await CreateThrowawayProductAsync("Delete Probe Product", token);

        var response = await ApiContext.DeleteAsync($"/products/{productId}");

        Assert.That(response.Status, Is.EqualTo(401));
        var body = (await response.JsonAsync())!.Value;
        Assert.That(body.GetProperty("message").GetString(), Is.EqualTo("Unauthorized"));

        await DeleteProductAsync(productId, token);
    }

    [Test]
    [AllureName("DELETE /products/{id} with admin auth returns 204 and the product is actually gone")]
    public async Task Delete_WithAuth_DeletesProduct()
    {
        var token = await GetAdminTokenAsync();
        var productId = await CreateThrowawayProductAsync("Delete Probe Product", token);

        var response = await DeleteProductAsync(productId, token);
        Assert.That(response.Status, Is.EqualTo(204));

        var reloaded = await ApiContext.GetAsync($"/products/{productId}");
        Assert.That(reloaded.Status, Is.EqualTo(404));
    }

    [Test]
    [AllureName("DELETE /products/{id} for a missing product returns 404, even with valid auth")]
    public async Task Delete_MissingProduct_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();

        var response = await DeleteProductAsync("does-not-exist", token);

        Assert.That(response.Status, Is.EqualTo(404));
    }
}
