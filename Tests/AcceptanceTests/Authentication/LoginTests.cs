using Allure.Net.Commons;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Bogus;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Actions;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;
using Toolshop.Playwright.Tests.Pages;
using Toolshop.Playwright.Tests.Utilities;

namespace Toolshop.Playwright.Tests.Tests.AcceptanceTests.Authentication;

/// <summary>
/// Ports AC1-AC7 of the "Registered customer logs in" story from
/// <c>ExampleMaps/login-user/authentication-login.map.final.md</c>.
/// </summary>
[AllureNUnit]
[AllureEpic("AcceptanceTests")]
[AllureFeature("Authentication")]
[Category("Authentication")]
[TestFixture]
public class LoginTests : ToolshopTest
{
    private static readonly Faker Faker = new();

    private LoginPage _loginPage = null!;
    private NavBar _navBar = null!;
    private AdminDashboardPage _adminDashboard = null!;
    private APIHelper _apiHelper = null!;
    private AuthenticationActions _authActions = null!;
    private string? _createdAccountId;

    [SetUp]
    public void SetUp()
    {
        _loginPage = new LoginPage(Page);
        _navBar = new NavBar(Page);
        _adminDashboard = new AdminDashboardPage(Page);
        _apiHelper = new APIHelper(ApiContext);
        _authActions = new AuthenticationActions(_loginPage, _apiHelper);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_createdAccountId is null) return;

        await AllureApi.Step("Cleanup: delete the throwaway account created for this test",
            () => _apiHelper.DeleteAccountAsync(_createdAccountId));
    }

    /// <summary>
    /// Builds a throwaway, registerable customer with unique, realistic details. No shared
    /// registration factory exists yet (this stage's own rule is to build data inline rather than
    /// presuppose one - see <c>test-data.instructions.md</c>); the real API's <c>StoreCustomer</c>
    /// request (read directly, not assumed) only requires first/last name, email and password - see
    /// <see cref="CustomerRegistration"/>. The password is randomly generated per call rather than a
    /// fixed value: the real API runs it through Laravel's <c>Password::uncompromised()</c>, a live
    /// breach-database check, and a hand-picked "look strong" password is exactly the kind of value
    /// likely to already be in a breach corpus.
    /// </summary>
    private static CustomerRegistration BuildThrowawayRegistration()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        return new CustomerRegistration(
            FirstName: Faker.Name.FirstName(),
            LastName: Faker.Name.LastName(),
            Dob: DateTime.UtcNow.AddYears(-30).ToString("yyyy-MM-dd"),
            Email: $"test+{uniqueId}@example.com",
            Password: $"Aa1!{uniqueId}");
    }

    // ==================== AC1 - Valid credentials log the user in ====================

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("A registered customer logging in with correct credentials is redirected to their account overview")]
    public async Task ValidCredentialsRedirectToAccountOverview()
    {
        await AllureApi.Step("Given a registered customer is on the login page",
            () => _loginPage.NavigateToAsync());

        await AllureApi.Step("When they submit their correct email and password",
            () => _loginPage.SubmitAsync(Accounts.Customer.Email, Accounts.Customer.Password));

        await AllureApi.Step("Then they are logged in and the nav bar shows their account menu",
            () => Assertions.Expect(_navBar.AccountMenuButton).ToHaveTextAsync(Accounts.Customer.Name));
    }

    // ==================== AC2 - Invalid credentials show an error ====================

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("Logging in with an incorrect password shows an invalid-credentials error")]
    public async Task InvalidCredentialsShowError()
    {
        await AllureApi.Step("Given a registered customer is on the login page",
            () => _loginPage.NavigateToAsync());

        await AllureApi.Step("When they submit their email with the wrong password",
            () => _loginPage.SubmitAsync(Accounts.Customer.Email, "wrong-password"));

        await AllureApi.Step("Then an invalid-credentials error is shown and they stay on the login page",
            () => Assertions.Expect(_loginPage.ErrorAlert).ToHaveTextAsync("Invalid email or password"));
    }

    // ==================== AC3 - Required fields are validated ====================

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("Submitting the login form with both fields empty shows a required-field error for each")]
    public async Task RequiredFieldsAreValidated()
    {
        await AllureApi.Step("Given a customer is on the login page",
            () => _loginPage.NavigateToAsync());

        await AllureApi.Step("When they submit the form without entering an email or password",
            () => _loginPage.ClickSubmitAsync());

        await AllureApi.Step("Then a required-field error is shown for each", async () =>
        {
            await Assertions.Expect(_loginPage.EmailError).ToHaveTextAsync("Email is required");
            await Assertions.Expect(_loginPage.PasswordError).ToHaveTextAsync("Password is required");
        });
    }

    // ==================== AC4 - Redirect target depends on role ====================

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("An admin logging in is sent to the admin dashboard instead of the account overview")]
    public async Task AdminLoginRedirectsToDashboard()
    {
        await AllureApi.Step("Given the admin account is on the login page",
            () => _loginPage.NavigateToAsync());

        await AllureApi.Step("When they submit their correct email and password",
            () => _loginPage.SubmitAsync(Accounts.Admin.Email, Accounts.Admin.Password));

        await AllureApi.Step("Then they are redirected to the admin dashboard",
            () => Assertions.Expect(_adminDashboard.PageTitle).ToHaveTextAsync("Sales over the years"));
    }

    // ==================== AC5 - Accounts with TOTP enabled require a verification code ====================

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("Submitting the correct TOTP code after a correct password completes sign-in")]
    public async Task CorrectTotpCodeCompletesLogin()
    {
        CustomerRegistration registration = null!;
        var totpSecret = string.Empty;

        await AllureApi.Step("Given a throwaway account with TOTP enabled", async () =>
        {
            registration = BuildThrowawayRegistration();
            var setup = await _authActions.RegisterAndEnableTotpAsync(registration);
            _createdAccountId = setup.AccountId;
            totpSecret = setup.TotpSecret;
        });

        await AllureApi.Step("When they submit correct credentials, then the current, correctly-computed TOTP code", async () =>
        {
            await _loginPage.SubmitAsync(registration.Email, registration.Password);
            var code = TotpCodeGenerator.ComputeCurrentCode(totpSecret);
            await _loginPage.SubmitTotpCodeAsync(code);
        });

        await AllureApi.Step("Then they are signed in",
            () => Assertions.Expect(_navBar.AccountMenuButton)
                .ToHaveTextAsync($"{registration.FirstName} {registration.LastName}"));
    }

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("Submitting an incorrect TOTP code shows an error and does not sign the customer in")]
    public async Task IncorrectTotpCodeShowsError()
    {
        CustomerRegistration registration = null!;

        await AllureApi.Step("Given a throwaway account with TOTP enabled", async () =>
        {
            registration = BuildThrowawayRegistration();
            var setup = await _authActions.RegisterAndEnableTotpAsync(registration);
            _createdAccountId = setup.AccountId;
        });

        await AllureApi.Step("When they submit correct credentials, then an incorrect TOTP code", async () =>
        {
            await _loginPage.SubmitAsync(registration.Email, registration.Password);
            await _loginPage.SubmitTotpCodeAsync("000000");
        });

        await AllureApi.Step("Then an invalid-TOTP error is shown",
            () => Assertions.Expect(_loginPage.ErrorAlert).ToHaveTextAsync("Invalid TOTP"));
    }

    // ==================== AC6 - Account is locked after repeated failed attempts ====================

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("Three consecutive failed attempts lock the account, even for the next correct attempt")]
    public async Task ThreeFailedAttemptsLockTheAccount()
    {
        CustomerRegistration registration = null!;

        await AllureApi.Step("Given a throwaway non-admin account", async () =>
        {
            registration = BuildThrowawayRegistration();
            _createdAccountId = await _apiHelper.RegisterCustomerAsync(registration);
            await _loginPage.NavigateToAsync();
        });

        // There is no way to directly set failed_login_attempts; three real failures is the only
        // way to reach the locked state, so this covers the map's AC6 E1 and E2 together.
        await AllureApi.Step("When the account fails to log in three times in a row, then submits the correct password", async () =>
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await _loginPage.SubmitAsync(registration.Email, "wrong-password");
            }
            await _loginPage.SubmitAsync(registration.Email, registration.Password);
        });

        await AllureApi.Step("Then the account is locked out even though the password was correct",
            () => Assertions.Expect(_loginPage.ErrorAlert)
                .ToHaveTextAsync("Account locked, too many failed attempts. Please contact the administrator."));
    }

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("An admin account is exempt from the failed-attempt lockout rule")]
    public async Task AdminAccountIsExemptFromLockout()
    {
        // Safe to reuse the shared admin account: UserService never increments its failed-attempt
        // counter at all, by the same rule this test is verifying.
        await AllureApi.Step("Given the admin account has failed to log in three times in a row", async () =>
        {
            await _loginPage.NavigateToAsync();
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await _loginPage.SubmitAsync(Accounts.Admin.Email, "wrong-password");
            }
        });

        await AllureApi.Step("When the admin then submits the correct password",
            () => _loginPage.SubmitAsync(Accounts.Admin.Email, Accounts.Admin.Password));

        await AllureApi.Step("Then they are signed in, not locked out, and reach the admin dashboard",
            () => Assertions.Expect(_adminDashboard.PageTitle).ToHaveTextAsync("Sales over the years"));
    }

    // ==================== AC7 - Disabled accounts cannot log in ====================

    [Test]
    [AllureStory("LoginTests")]
    [AllureName("A disabled account is rejected even with the correct password")]
    public async Task DisabledAccountCannotLogIn()
    {
        CustomerRegistration registration = null!;

        await AllureApi.Step("Given a throwaway account that an admin has disabled", async () =>
        {
            registration = BuildThrowawayRegistration();
            _createdAccountId = await _apiHelper.RegisterCustomerAsync(registration);
            await _apiHelper.DisableAccountAsync(_createdAccountId);
            await _loginPage.NavigateToAsync();
        });

        await AllureApi.Step("When they submit their correct email and password",
            () => _loginPage.SubmitAsync(registration.Email, registration.Password));

        await AllureApi.Step("Then the login is rejected as disabled, not as invalid credentials",
            () => Assertions.Expect(_loginPage.ErrorAlert).ToHaveTextAsync("Account disabled"));
    }
}
