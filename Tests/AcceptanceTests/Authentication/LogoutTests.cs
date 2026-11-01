using Allure.Net.Commons;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;
using Toolshop.Playwright.Tests.Pages;

namespace Toolshop.Playwright.Tests.Tests.AcceptanceTests.Authentication;

/// <summary>
/// Ports AC1 of the "Registered customer logs out" story from
/// <c>ExampleMaps/login-user/authentication-login.map.final.md</c>.
/// </summary>
[AllureNUnit]
[AllureEpic("AcceptanceTests")]
[AllureFeature("Authentication")]
[Category("Authentication")]
[TestFixture]
public class LogoutTests : ToolshopTest
{
    private LoginPage _loginPage = null!;
    private NavBar _navBar = null!;

    [SetUp]
    public void SetUp()
    {
        _loginPage = new LoginPage(Page);
        _navBar = new NavBar(Page);
    }

    [Test]
    [AllureStory("LogoutTests")]
    [AllureName("Signing out ends the session and returns the customer to the login page")]
    public async Task SignOutReturnsToLoginPage()
    {
        await AllureApi.Step("Given a registered customer is logged in", async () =>
        {
            await _loginPage.NavigateToAsync();
            await _loginPage.SubmitAsync(Accounts.Customer.Email, Accounts.Customer.Password);
            await Assertions.Expect(_navBar.AccountMenuButton).ToBeVisibleAsync();
        });

        await AllureApi.Step("When they open the account menu and sign out", async () =>
        {
            await _navBar.OpenAccountMenuAsync();
            await _navBar.SignOutAsync();
        });

        await AllureApi.Step("Then they are returned to the login page, signed out",
            () => Assertions.Expect(_navBar.SignInLink).ToBeVisibleAsync());
    }
}
