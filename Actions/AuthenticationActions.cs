using Allure.Net.Commons.Attributes;
using Toolshop.Playwright.Tests.Fixtures;
using Toolshop.Playwright.Tests.Pages;

namespace Toolshop.Playwright.Tests.Actions;

/// <summary>What the caller couldn't have supplied itself - the API generates both.</summary>
public record TotpSetupResult(string AccountId, string TotpSecret);

public class AuthenticationActions(LoginPage loginPage, APIHelper apiHelper)
{
    /// <summary>
    /// Registers the given throwaway customer and enables TOTP for them, leaving the browser on
    /// the login page ready to sign in. Used by two tests (correct- and incorrect-TOTP-code), which
    /// is what promoted this out of being inlined in either one - see
    /// <c>common-actions.instructions.md</c>.
    /// </summary>
    [AllureStep]
    public async Task<TotpSetupResult> RegisterAndEnableTotpAsync(CustomerRegistration registration)
    {
        var accountId = await apiHelper.RegisterCustomerAsync(registration);
        var totpSecret = await apiHelper.EnableTotpAsync(registration.Email, registration.Password);
        await loginPage.NavigateToAsync();

        return new TotpSetupResult(accountId, totpSecret);
    }
}
