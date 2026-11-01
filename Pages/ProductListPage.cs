using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Pages;

/// <summary>
/// The product catalog / search results page (home page). Mirrors the Java
/// <c>ProductList</c>/<c>Search</c> page objects and the TypeScript <c>CatalogPage</c> helper.
/// </summary>
public class ProductListPage(IPage page)
{
    // ==================== Locators ====================

    public ILocator SearchInput => page.GetByTestId("search-query");

    public ILocator SearchButton => page.GetByTestId("search-submit");

    public ILocator SearchResetButton => page.GetByTestId("search-reset");

    public ILocator ProductCards => page.Locator("a[data-test^='product-']");

    public ILocator ProductNames => page.GetByTestId("product-name");

    // ==================== Interaction Methods ====================

    public ILocator GetProductCard(string name) =>
        ProductCards.Filter(new LocatorFilterOptions { HasText = name });

    public ILocator GetProductCardName(string name) =>
        GetProductCard(name).GetByTestId("product-name");

    public ILocator GetProductCardPrice(string name) =>
        GetProductCard(name).GetByTestId("product-price");

    // ==================== Action Methods ====================

    public async Task NavigateToAsync()
    {
        await page.GotoAsync("/");
    }

    public async Task SearchForAsync(string query)
    {
        await SearchInput.FillAsync(query);
        await Task.WhenAll(
            page.WaitForResponseAsync(r => r.Url.Contains("/products") && r.Url.Contains("search")),
            SearchButton.ClickAsync());
    }

    public async Task ClearSearchAsync()
    {
        await Task.WhenAll(
            page.WaitForResponseAsync(r => r.Url.Contains("/products")),
            SearchResetButton.ClickAsync());
    }

    public async Task<IReadOnlyList<string>> GetProductNamesAsync() => await ProductNames.AllInnerTextsAsync();
}
