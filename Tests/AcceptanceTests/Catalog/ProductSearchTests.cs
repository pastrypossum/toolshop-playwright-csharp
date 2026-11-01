using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Fixtures;
using Toolshop.Playwright.Tests.Pages;

namespace Toolshop.Playwright.Tests.Tests.AcceptanceTests.Catalog;

/// <summary>
/// Basic product-search coverage, mirroring Java's <c>Search</c>/<c>ProductList</c> page
/// objects and the TypeScript <c>CatalogPage</c> search helpers.
/// </summary>
[AllureNUnit]
[AllureEpic("AcceptanceTests")]
[AllureFeature("Browse product catalog")]
[TestFixture]
public class ProductSearchTests : ToolshopTest
{
    private ProductListPage _productList = null!;

    [SetUp]
    public void SetUp()
    {
        _productList = new ProductListPage(Page);
    }

    [Test]
    [AllureName("The home page displays the product catalog with at least one product")]
    public async Task HomePageDisplaysProductCatalog()
    {
        await _productList.NavigateToAsync();

        await Assertions.Expect(_productList.ProductCards.First).ToBeVisibleAsync();
    }

    [Test]
    [AllureName("Searching for \"Pliers\" returns only matching products")]
    public async Task SearchingForKeywordReturnsMatchingProducts()
    {
        await _productList.NavigateToAsync();

        await _productList.SearchForAsync("Pliers");

        var names = await _productList.GetProductNamesAsync();

        Assert.That(names, Is.Not.Empty);
        Assert.That(names, Has.All.Matches<string>(n => n.Contains("Pliers", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    [AllureName("Searching for a keyword with no matches shows an empty catalog")]
    public async Task SearchingForUnknownKeywordReturnsNoProducts()
    {
        await _productList.NavigateToAsync();

        await _productList.SearchForAsync("zzz-no-such-product-zzz");

        var names = await _productList.GetProductNamesAsync();

        Assert.That(names, Is.Empty);
    }
}
