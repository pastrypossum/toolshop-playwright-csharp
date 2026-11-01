using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Pages;

public class LoginPage(IPage page)
{
    // ==================== Locators ====================

    public ILocator EmailInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Email address" });

    // type="password" has no mapped ARIA role - Label is the next rung, not TestId. A substring
    // match on "Password" is ambiguous here: the page also has a "Forgot your Password?" link
    // whose aria-label contains the same substring, so this needs an exact match on the real
    // label text (confirmed from login.component.html: "Password" + " *" required-marker).
    public ILocator PasswordInput => page.GetByLabel("Password *", new() { Exact = true });

    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Login" });

    public ILocator TotpInput => page.GetByRole(AriaRole.Textbox, new() { Name = "TOTP Code" });

    public ILocator VerifyTotpButton => page.GetByRole(AriaRole.Button, new() { Name = "Verify TOTP" });

    // ==================== Feedback Locators ====================

    // None of these three carry a mapped role (aria-live isn't a role) and their text is the exact
    // thing each test is about to assert - Role/Text would be either absent or circular. TestId is
    // the correct rung here, not a shortcut past it.
    public ILocator EmailError => page.GetByTestId("email-error");

    public ILocator PasswordError => page.GetByTestId("password-error");

    public ILocator ErrorAlert => page.GetByTestId("login-error");

    // ==================== Action Methods ====================

    public async Task NavigateToAsync() => await page.GotoAsync("/auth/login");

    public async Task SubmitAsync(string email, string password)
    {
        await EmailInput.FillAsync(email);
        await PasswordInput.FillAsync(password);
        await page.RunAndWaitForResponseAsync(
            () => SubmitButton.ClickAsync(),
            r => r.Url.Contains("/users/login"));
    }

    public async Task ClickSubmitAsync() => await SubmitButton.ClickAsync();

    public async Task SubmitTotpCodeAsync(string code)
    {
        await TotpInput.FillAsync(code);
        await page.RunAndWaitForResponseAsync(
            () => VerifyTotpButton.ClickAsync(),
            r => r.Url.Contains("/users/login"));
    }
}
