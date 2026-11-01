using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Pages;

public class AdminDashboardPage(IPage page)
{
    // ==================== Locators ====================

    // No Name filter: the test's own assertion checks the exact text ("Sales over the years") -
    // baking that same string into the locator too would make the assertion unable to fail.
    public ILocator PageTitle => page.GetByRole(AriaRole.Heading, new() { Level = 1 });

    // ==================== Action Methods ====================

    public async Task NavigateToAsync() => await page.GotoAsync("/admin/dashboard");
}
