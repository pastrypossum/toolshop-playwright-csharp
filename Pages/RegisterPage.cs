using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Pages;

/// <summary>
/// The full registration form's field values. Every property defaults to empty so a test can start
/// from <see cref="RegisterPage.ValidBaseline"/> and override just the one field it's varying
/// (<c>with { FirstName = "" }</c>) - see the AC2/AC6/AC8/AC9 tests in <c>RegistrationTests.cs</c>.
/// </summary>
public record RegistrationFormData(
    string FirstName,
    string LastName,
    string Dob,
    string Country,
    string PostalCode,
    string HouseNumber,
    string Street,
    string City,
    string State,
    string Phone,
    string Email,
    string Password);

public class RegisterPage(IPage page)
{
    // ==================== Locators ====================

    // type="text" maps to role textbox; every field below has a real <label for="...">, so Role
    // applies cleanly - confirmed by running the suite, not assumed.
    public ILocator FirstNameInput => page.GetByRole(AriaRole.Textbox, new() { Name = "First name" });
    public ILocator LastNameInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Last name" });
    public ILocator DobInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Date of Birth" });
    public ILocator CountrySelect => page.GetByRole(AriaRole.Combobox, new() { Name = "Country" });
    public ILocator PostalCodeInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Postal code" });
    public ILocator HouseNumberInput => page.GetByRole(AriaRole.Textbox, new() { Name = "House number" });
    public ILocator StreetInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Street" });
    public ILocator CityInput => page.GetByRole(AriaRole.Textbox, new() { Name = "City" });
    public ILocator StateInput => page.GetByRole(AriaRole.Textbox, new() { Name = "State" });
    public ILocator PhoneInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Phone" });
    public ILocator EmailInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Email address" });

    // type="password" has no mapped ARIA role - see page-object.instructions.md.
    public ILocator PasswordInput => page.GetByLabel("Password", new() { Exact = true });

    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Register" });

    // ==================== Feedback Locators ====================

    // These DO carry role="alert" (confirmed from the template) - but several can render at once
    // (e.g. submitting a fully empty form shows every field's error together), all with the same
    // role and no distinguishing accessible name, so Role alone can't pick one out. TestId is the
    // right rung here for that reason, not because no role exists - don't copy the "no role" framing
    // onto a case where the real reason is ambiguity. ServerError is the one locator below that
    // genuinely has no role at all.
    public ILocator FirstNameError => page.GetByTestId("first-name-error");
    public ILocator LastNameError => page.GetByTestId("last-name-error");
    public ILocator DobError => page.GetByTestId("dob-error");
    public ILocator CountryError => page.GetByTestId("country-error");
    public ILocator PostalCodeError => page.GetByTestId("postal_code-error");
    public ILocator HouseNumberError => page.GetByTestId("house_number-error");
    public ILocator StreetError => page.GetByTestId("street-error");
    public ILocator CityError => page.GetByTestId("city-error");
    public ILocator StateError => page.GetByTestId("state-error");
    public ILocator PhoneError => page.GetByTestId("phone-error");
    public ILocator EmailError => page.GetByTestId("email-error");
    public ILocator PasswordError => page.GetByTestId("password-error");

    /// <summary>The server-side rejection banner - distinct from the per-field client errors above.</summary>
    public ILocator ServerError => page.GetByTestId("register-error");

    // ==================== Action Methods ====================

    public async Task NavigateToAsync() => await page.GotoAsync("/auth/register");

    /// <summary>
    /// Fills every field from <paramref name="data"/>. A blank value leaves/clears that field -
    /// this is how AC2's required-field Examples omit exactly one field. The country select is
    /// only touched when a value is given, so AC2-E4 (no country selected) is expressible too.
    /// </summary>
    public async Task FillAsync(RegistrationFormData data)
    {
        await FirstNameInput.FillAsync(data.FirstName);
        await LastNameInput.FillAsync(data.LastName);
        await DobInput.FillAsync(data.Dob);
        if (!string.IsNullOrEmpty(data.Country))
        {
            await CountrySelect.SelectOptionAsync(data.Country);
        }
        await PostalCodeInput.FillAsync(data.PostalCode);
        await HouseNumberInput.FillAsync(data.HouseNumber);
        await StreetInput.FillAsync(data.Street);
        await CityInput.FillAsync(data.City);
        await StateInput.FillAsync(data.State);
        await PhoneInput.FillAsync(data.Phone);
        await EmailInput.FillAsync(data.Email);
        await PasswordInput.FillAsync(data.Password);
    }

    /// <summary>For a submission the client itself blocks (a required field is missing) - no network call to wait for.</summary>
    public async Task ClickSubmitAsync() => await SubmitButton.ClickAsync();

    /// <summary>For a submission that reaches the API - waits for the <c>/users/register</c> response.</summary>
    public async Task SubmitAsync() =>
        await page.RunAndWaitForResponseAsync(
            () => SubmitButton.ClickAsync(),
            r => r.Url.Contains("/users/register"));
}
