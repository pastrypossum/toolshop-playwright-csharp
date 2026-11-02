using Allure.Net.Commons;
using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Bogus;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;
using Toolshop.Playwright.Tests.Pages;

namespace Toolshop.Playwright.Tests.Tests.AcceptanceTests.Registration;

/// <summary>
/// Ports AC1-AC9 of the "Guest creates an account" story from
/// <c>ExampleMaps/register-user/registration.map.final.md</c>.
/// </summary>
[AllureNUnit]
[AllureEpic("AcceptanceTests")]
[AllureFeature("Registration")]
[Category("Registration")]
[TestFixture]
public class RegistrationTests : ToolshopTest
{
    private static readonly Faker Faker = new();

    private RegisterPage _registerPage = null!;
    private LoginPage _loginPage = null!;
    private NavBar _navBar = null!;
    private APIHelper _apiHelper = null!;
    private string? _createdAccountEmail;

    [SetUp]
    public void SetUp()
    {
        _registerPage = new RegisterPage(Page);
        _loginPage = new LoginPage(Page);
        _navBar = new NavBar(Page);
        _apiHelper = new APIHelper(ApiContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_createdAccountEmail is null) return;

        await AllureApi.Step("Cleanup: delete the account this test registered", async () =>
        {
            var id = await _apiHelper.FindUserIdByEmailAsync(_createdAccountEmail);
            if (id is not null)
            {
                await _apiHelper.DeleteAccountAsync(id);
            }
        });
    }

    /// <summary>
    /// A fully valid registration, as a baseline - override just the field(s) a test is varying
    /// (<c>ValidBaseline() with { FirstName = "" }</c>). Germany/10115 is a real, format-valid
    /// country+postcode combination (confirmed from register.component.ts's own postcodePatterns),
    /// not a geocoded real address - the backend only checks shape, not that the address exists.
    /// </summary>
    private static RegistrationFormData ValidBaseline()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        return new RegistrationFormData(
            FirstName: Faker.Name.FirstName(),
            LastName: Faker.Name.LastName(),
            Dob: DateTime.UtcNow.AddYears(-30).ToString("yyyy-MM-dd"),
            Country: "DE",
            PostalCode: "10115",
            HouseNumber: "1",
            Street: Faker.Address.StreetName(),
            City: "Berlin",
            State: "Berlin",
            Phone: Faker.Random.Number(100000000, 199999999).ToString(),
            Email: $"test+{uniqueId}@example.com",
            Password: $"Aa1!{uniqueId}");
    }

    // ==================== AC1 - Valid details create a working account ====================

    [Test]
    [AllureName("A guest registering with valid details can log in with the new account")]
    public async Task ValidDetailsCreateAWorkingAccount()
    {
        var data = ValidBaseline();

        await AllureApi.Step("Given a guest fills in all required registration fields with valid data", async () =>
        {
            await _registerPage.NavigateToAsync();
            await _registerPage.FillAsync(data);
        });

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            await _registerPage.SubmitAsync();
            _createdAccountEmail = data.Email;
        });

        await AllureApi.Step("Then the new account can actually log in with those credentials", async () =>
        {
            // Registration itself redirects to /auth/login on success (register.component.ts's
            // redirectToLogin()) - navigating there again would race that in-flight navigation.
            // The login form's own locators auto-wait for it to actually arrive.
            await _loginPage.SubmitAsync(data.Email, data.Password);
            await Assertions.Expect(_navBar.AccountMenuButton).ToHaveTextAsync($"{data.FirstName} {data.LastName}");
        });
    }

    // ==================== AC2 - Required fields are validated ====================

    private static IEnumerable<TestCaseData> RequiredFieldCases()
    {
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { FirstName = "" }),
            (Func<RegisterPage, ILocator>)(p => p.FirstNameError),
            "First name is required").SetName("RequiredFieldIsValidated(FirstName)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { LastName = "" }),
            (Func<RegisterPage, ILocator>)(p => p.LastNameError),
            "Last name is required").SetName("RequiredFieldIsValidated(LastName)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { Dob = "" }),
            (Func<RegisterPage, ILocator>)(p => p.DobError),
            "Date of Birth is required").SetName("RequiredFieldIsValidated(Dob)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { Country = "" }),
            (Func<RegisterPage, ILocator>)(p => p.CountryError),
            "Country is required").SetName("RequiredFieldIsValidated(Country)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { PostalCode = "" }),
            (Func<RegisterPage, ILocator>)(p => p.PostalCodeError),
            "Postcode is required").SetName("RequiredFieldIsValidated(PostalCode)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { HouseNumber = "" }),
            (Func<RegisterPage, ILocator>)(p => p.HouseNumberError),
            "House number is required").SetName("RequiredFieldIsValidated(HouseNumber)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { Street = "" }),
            (Func<RegisterPage, ILocator>)(p => p.StreetError),
            "Street is required").SetName("RequiredFieldIsValidated(Street)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { City = "" }),
            (Func<RegisterPage, ILocator>)(p => p.CityError),
            "City is required").SetName("RequiredFieldIsValidated(City)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { State = "" }),
            (Func<RegisterPage, ILocator>)(p => p.StateError),
            "State is required").SetName("RequiredFieldIsValidated(State)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { Phone = "" }),
            (Func<RegisterPage, ILocator>)(p => p.PhoneError),
            "Phone is required.").SetName("RequiredFieldIsValidated(Phone)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { Email = "" }),
            (Func<RegisterPage, ILocator>)(p => p.EmailError),
            "Email is required").SetName("RequiredFieldIsValidated(Email)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { Password = "" }),
            (Func<RegisterPage, ILocator>)(p => p.PasswordError),
            "Password is required").SetName("RequiredFieldIsValidated(Password)");
    }

    [TestCaseSource(nameof(RequiredFieldCases))]
    public async Task RequiredFieldIsValidated(
        Func<RegistrationFormData, RegistrationFormData> withFieldEmpty,
        Func<RegisterPage, ILocator> errorLocator,
        string expectedMessage)
    {
        await AllureApi.Step("Given a guest leaves a required field empty",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            await _registerPage.FillAsync(withFieldEmpty(ValidBaseline()));
            await _registerPage.ClickSubmitAsync();
        });

        // ToContainTextAsync, not ToHaveTextAsync: Dob and Password fail more than one client
        // validator simultaneously on an empty value (e.g. dob also fails its date-format check),
        // so more than one message can render together - the required message being present is
        // what this AC claims, not that it's the only message shown.
        await AllureApi.Step("Then a required-field error is shown for that field",
            () => Assertions.Expect(errorLocator(_registerPage)).ToContainTextAsync(expectedMessage));
    }

    // ==================== AC3 - Password must meet complexity rules ====================

    [TestCase("abc", "Password must be minimal 6 characters long.")]
    [TestCase("Abc123!", "Password must be minimal 6 characters long.")]
    public async Task ShortPasswordIsRejected(string password, string expectedMessage)
    {
        await AllureApi.Step("Given a guest enters a password that doesn't meet the length rule",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            await _registerPage.FillAsync(ValidBaseline() with { Password = password });
            await _registerPage.ClickSubmitAsync();
        });

        await AllureApi.Step("Then a password error is shown describing what's missing",
            () => Assertions.Expect(_registerPage.PasswordError).ToContainTextAsync(expectedMessage));
    }

    [Test]
    [AllureName("A password meeting the real 8-character minimum registers successfully")]
    public async Task EightCharacterPasswordPassesLengthCheck()
    {
        // Length is enforced both client AND server-side, so this tests through to the real
        // backend like any other Example (writing-acceptance-tests.instructions.md) - it can't use
        // AC7's own breached password (Abcd123!) for that reason, so this is a different, unique,
        // non-breached 8-character password - the boundary value itself, not a longer one, since
        // what's under test is specifically the 8-char minimum (contrast with AC3's 7-char E2).
        RegistrationFormData data = null!;

        await AllureApi.Step("Given a guest enters an 8-character password meeting every complexity rule",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            var uniqueId = Guid.NewGuid().ToString("N")[..4];
            data = ValidBaseline() with { Password = $"Aa1!{uniqueId}" };
            await _registerPage.FillAsync(data);
            await _registerPage.SubmitAsync();
            _createdAccountEmail = data.Email;
        });

        // Once redirected away, the register page's own error elements simply don't exist anymore -
        // checking they're absent would be near-tautological, not real proof. A real login is the
        // same tangible proof AC1 uses (writing-acceptance-tests.instructions.md's assertion rule).
        await AllureApi.Step("Then the registration succeeds - the 8-character minimum is met", async () =>
        {
            await _loginPage.SubmitAsync(data.Email, data.Password);
            await Assertions.Expect(_navBar.AccountMenuButton).ToHaveTextAsync($"{data.FirstName} {data.LastName}");
        });
    }

    // ==================== AC4 - Postal code lookup fills in address fields ====================

    [Test]
    [AllureName("Entering a valid country, postal code and house number auto-fills street/city/state")]
    public async Task PostcodeLookupAutoFillsAddress()
    {
        await AllureApi.Step("Given a guest is on the registration form",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they enter a valid country, postal code and house number", async () =>
        {
            // The lookup only fires once all three fields have a value (confirmed live via the
            // network tab, not assumed) - plain fills, then let the auto-retrying assertion below
            // wait for the debounced result, rather than trying to guess which field's own change
            // is the one that triggers the request.
            await _registerPage.CountrySelect.SelectOptionAsync("NL");
            await _registerPage.PostalCodeInput.FillAsync("1011AB");
            await _registerPage.HouseNumberInput.FillAsync("1");
        });

        // Real values confirmed live for country=NL/postcode=1011AB/house_number=1 - a concrete
        // example, not a vague "something got filled in" check.
        await AllureApi.Step("Then the street, city and state fields are automatically filled in", async () =>
        {
            await Assertions.Expect(_registerPage.StreetInput).ToHaveValueAsync("Autarboulevard");
            await Assertions.Expect(_registerPage.CityInput).ToHaveValueAsync("Stroe");
            await Assertions.Expect(_registerPage.StateInput).ToHaveValueAsync("Groningen");
        });
    }

    // ==================== AC5 - Duplicate email is rejected ====================

    [Test]
    [AllureName("Registering with an email that's already in use is rejected")]
    public async Task DuplicateEmailIsRejected()
    {
        await AllureApi.Step("Given a guest registers with an email that's already in use",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            await _registerPage.FillAsync(ValidBaseline() with { Email = Accounts.Customer.Email });
            await _registerPage.SubmitAsync();
        });

        await AllureApi.Step("Then an error is shown and no account is created",
            () => Assertions.Expect(_registerPage.ServerError)
                .ToContainTextAsync("A customer with this email address already exists."));
    }

    // ==================== AC6 - Customers must be between 18 and 75 years old ====================

    [Test]
    [AllureName("A customer younger than 18 is rejected at registration")]
    public async Task TooYoungIsRejected()
    {
        await AllureApi.Step("Given a guest's date of birth makes them younger than 18",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            var dob = DateTime.UtcNow.AddYears(-6).ToString("yyyy-MM-dd");
            await _registerPage.FillAsync(ValidBaseline() with { Dob = dob });
            await _registerPage.SubmitAsync();
        });

        await AllureApi.Step("Then the registration is rejected with an age-specific error",
            () => Assertions.Expect(_registerPage.ServerError).ToContainTextAsync("Customer must be 18 years old."));
    }

    /// <summary>
    /// KNOWN GAP, confirmed live via direct API calls (not assumed from source): the "customer
    /// must be younger than 75" rule in <c>StoreCustomer::rules()</c> does not actually reject
    /// anyone - dob values for age 75, 76 and 80 all registered successfully (201) when probed
    /// directly. This test asserts the *intended* behaviour from the map/source and is expected to
    /// fail until the real bug is fixed - kept separate from <see cref="TooYoungIsRejected"/> so
    /// that test can stay meaningfully green, per <c>writing-acceptance-tests.instructions.md</c>'s
    /// known-gap convention. Recorded in <c>ExampleMaps/known-gaps.md</c>.
    /// </summary>
    [Test]
    [AllureName("KNOWN GAP: a customer older than 75 should be rejected at registration, but isn't")]
    public async Task TooOldIsNotActuallyRejected()
    {
        await AllureApi.Step("Given a guest's date of birth makes them older than 75", async () =>
        {
            await _registerPage.NavigateToAsync();
        });

        RegistrationFormData data = null!;
        await AllureApi.Step("When they submit the registration form", async () =>
        {
            data = ValidBaseline() with { Dob = DateTime.UtcNow.AddYears(-80).ToString("yyyy-MM-dd") };
            await _registerPage.FillAsync(data);
            await _registerPage.SubmitAsync();
            // The bug means this actually succeeds and creates a real account - track it for
            // cleanup regardless of whether the assertion below passes or fails.
            _createdAccountEmail = data.Email;
        });

        await AllureApi.Step("Then the registration is rejected with an age-specific error",
            () => Assertions.Expect(_registerPage.ServerError).ToContainTextAsync("Customer must be younger than 75 years old."));
    }

    // ==================== AC7 - Breached passwords are rejected ====================

    [Test]
    [AllureName("A password that meets the complexity rules but is known-breached is rejected")]
    public async Task BreachedPasswordIsRejected()
    {
        await AllureApi.Step("Given a guest enters a password that meets the rules but is known-breached",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            await _registerPage.FillAsync(ValidBaseline() with { Password = "Abcd123!" });
            await _registerPage.SubmitAsync();
        });

        await AllureApi.Step("Then the registration is rejected with a breach-specific error",
            () => Assertions.Expect(_registerPage.ServerError)
                .ToContainTextAsync("The given password has appeared in a data leak. Please choose a different password."));
    }

    // ==================== AC8 - Field values are length-limited ====================

    private static IEnumerable<TestCaseData> FieldTooLongCases()
    {
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { FirstName = new string('A', 41) }),
            "The first name field must not be greater than 40 characters.")
            .SetName("FieldLongerThanMaximumIsRejected(FirstName)");
        yield return new TestCaseData(
            (Func<RegistrationFormData, RegistrationFormData>)(d => d with { LastName = new string('A', 21) }),
            "The last name field must not be greater than 20 characters.")
            .SetName("FieldLongerThanMaximumIsRejected(LastName)");
    }

    [TestCaseSource(nameof(FieldTooLongCases))]
    public async Task FieldLongerThanMaximumIsRejected(Func<RegistrationFormData, RegistrationFormData> withFieldTooLong, string expectedMessage)
    {
        await AllureApi.Step("Given a guest enters a value longer than a field's allowed maximum",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            await _registerPage.FillAsync(withFieldTooLong(ValidBaseline()));
            await _registerPage.SubmitAsync();
        });

        await AllureApi.Step("Then the registration is rejected",
            () => Assertions.Expect(_registerPage.ServerError).ToContainTextAsync(expectedMessage));
    }

    // ==================== AC9 - Subscript/superscript characters are rejected ====================

    [Test]
    [AllureName("A subscript/superscript character in a text field is rejected")]
    public async Task SubscriptCharacterIsRejected()
    {
        await AllureApi.Step("Given a guest enters a superscript character in the first name field",
            () => _registerPage.NavigateToAsync());

        await AllureApi.Step("When they submit the registration form", async () =>
        {
            await _registerPage.FillAsync(ValidBaseline() with { FirstName = "Pilot⁵" });
            await _registerPage.SubmitAsync();
        });

        await AllureApi.Step("Then the registration is rejected naming the offending field",
            () => Assertions.Expect(_registerPage.ServerError)
                .ToContainTextAsync("The first_name must not contain subscript/superscript characters."));
    }
}
