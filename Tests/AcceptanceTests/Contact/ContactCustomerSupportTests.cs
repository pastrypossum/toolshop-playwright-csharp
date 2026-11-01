using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Contact;
using Toolshop.Playwright.Tests.Fixtures;
using Toolshop.Playwright.Tests.Pages;

namespace Toolshop.Playwright.Tests.Tests.AcceptanceTests.Contact;

/// <summary>
/// Ports the "contact customer support" scenario covered by Java's
/// <c>ContactTests</c> and TypeScript's <c>as-guest.spec.ts</c>: a guest navigates to the
/// contact form via the nav bar, submits a valid question, and sees an acknowledgement;
/// the required-field and message-length validations are also covered.
/// </summary>
[AllureNUnit]
[AllureEpic("AcceptanceTests")]
[AllureFeature("Contact customer support")]
[TestFixture]
public class ContactCustomerSupportTests : ToolshopTest
{
    private NavBar _navBar = null!;
    private ContactPage _contactForm = null!;

    [SetUp]
    public void SetUp()
    {
        _navBar = new NavBar(Page);
        _contactForm = new ContactPage(Page);
    }

    [Test]
    [AllureName("Navigating to Contact via the nav bar displays the form with all required fields")]
    public async Task NavigateToContactFormDisplaysAllRequiredFields()
    {
        // Given a guest is viewing the homepage
        await Page.GotoAsync("/");
        await Assertions.Expect(_navBar.ContactLink).ToBeVisibleAsync();

        // When they navigate to Contact via the nav bar
        await _navBar.NavigateToContactAsync();

        // Then the contact form is displayed with all required fields
        await Assertions.Expect(_contactForm.FirstNameInput).ToBeVisibleAsync();
        await Assertions.Expect(_contactForm.LastNameInput).ToBeVisibleAsync();
        await Assertions.Expect(_contactForm.EmailInput).ToBeVisibleAsync();
        await Assertions.Expect(_contactForm.SubjectSelect).ToBeVisibleAsync();
        await Assertions.Expect(_contactForm.MessageTextarea).ToBeVisibleAsync();
        await Assertions.Expect(_contactForm.AttachmentInput).ToBeVisibleAsync();
    }

    [Test]
    [AllureName("Guest submits a valid contact form and sees an acknowledgement message")]
    public async Task GuestSubmitsValidContactFormAndSeesAcknowledgement()
    {
        await _contactForm.NavigateToAsync();

        var data = ContactMessageFactory.BuildGuestContactMessage();

        await _contactForm.SubmitAsGuestAsync(
            data.FirstName, data.LastName, data.Email, data.Subject, data.Message);

        await Assertions.Expect(_contactForm.SuccessAlert).ToHaveTextAsync(ContactMessages.Success);
    }

    [Test]
    [AllureName("Submitting the contact form without a subject shows a required field error")]
    public async Task MissingSubjectShowsRequiredFieldError()
    {
        await _contactForm.NavigateToAsync();

        var data = ContactMessageFactory.BuildGuestContactMessage();

        await _contactForm.FillFirstNameAsync(data.FirstName);
        await _contactForm.FillLastNameAsync(data.LastName);
        await _contactForm.FillEmailAsync(data.Email);
        await _contactForm.FillMessageAsync(data.Message);

        await _contactForm.ClickSubmitAsync();

        await Assertions.Expect(_contactForm.SubjectError).ToBeVisibleAsync();
    }

    [Test]
    [AllureName("Submitting a message shorter than 50 characters shows a length error")]
    public async Task MessageTooShortShowsLengthError()
    {
        await _contactForm.NavigateToAsync();

        var data = ContactMessageFactory.BuildGuestContactMessage();

        await _contactForm.FillOutAsGuestAsync(data.FirstName, data.LastName, data.Email, data.Subject, "Too short");
        await _contactForm.ClickSubmitAsync();

        await Assertions.Expect(_contactForm.MessageError).ToHaveTextAsync(ContactMessages.MessageTooShort);
    }

    [Test]
    [AllureName("Guest submits a contact form with a valid attachment and sees an acknowledgement message")]
    public async Task GuestSubmitsContactFormWithAttachmentAndSeesAcknowledgement()
    {
        await _contactForm.NavigateToAsync();

        var data = ContactMessageFactory.BuildGuestContactMessage();
        var attachment = Path.Combine(AppContext.BaseDirectory, "Data", "Fixtures", "valid-example-file.txt");

        await _contactForm.SubmitAsGuestWithAttachmentAsync(
            data.FirstName, data.LastName, data.Email, data.Subject, data.Message, attachment);

        await Assertions.Expect(_contactForm.SuccessAlert).ToHaveTextAsync(ContactMessages.Success);
    }
}
