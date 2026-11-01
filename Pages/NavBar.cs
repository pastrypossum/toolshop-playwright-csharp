using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Pages;

/// <summary>
/// The site-wide navigation bar. Mirrors the Java <c>NavBar</c> page object and the
/// TypeScript <c>NavPage</c> helper.
/// </summary>
public class NavBar(IPage page)
{
    // ==================== Locators ====================

    public ILocator ContactLink => page.GetByTestId("nav-contact");

    public ILocator HomeLink => page.GetByTestId("nav-home");

    // The dropdown toggle's accessible name is whichever account's name is currently signed in -
    // there's no fixed Name to query by, and "Select language" is another same-role button on the
    // same bar, so Role alone would be ambiguous. TestId is the right rung here, not a shortcut.
    public ILocator AccountMenuButton => page.GetByTestId("nav-menu");

    public ILocator SignInLink => page.GetByRole(AriaRole.Link, new() { Name = "Sign in" });

    // This anchor has (click)="logout()" with no href - unlike SignInLink's routerLink (which
    // Angular renders as a real href), an <a> with no href gets no implicit ARIA role at all, so
    // GetByRole(Link) never finds it. Confirmed by running it, not guessed. Falls to GetByText
    // (role doesn't apply here) - only one such element renders at a time (customer/admin menus
    // are mutually exclusive via @if), so it's still unambiguous.
    /// <summary>Only visible once <see cref="OpenAccountMenuAsync"/> has opened the dropdown.</summary>
    public ILocator SignOutLink => page.GetByText("Sign out");

    // ==================== Action Methods ====================

    public async Task NavigateToContactAsync()
    {
        await ContactLink.ClickAsync();
        await page.WaitForURLAsync("**/contact");
    }

    public async Task OpenAccountMenuAsync() => await AccountMenuButton.ClickAsync();

    public async Task SignOutAsync() => await SignOutLink.ClickAsync();
}
