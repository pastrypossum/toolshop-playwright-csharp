using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Pages;

/// <summary>
/// The "Contact" / customer support form. Mirrors the Java <c>Contact</c> page object
/// and the TypeScript <c>ContactPage</c> helper.
/// </summary>
public class ContactPage(IPage page)
{
    // ==================== Locators (guest-only fields) ====================
    // Hidden for registered users, whose name/email are pre-filled server-side.

    public ILocator FirstNameInput => page.GetByTestId("first-name");

    public ILocator LastNameInput => page.GetByTestId("last-name");

    public ILocator EmailInput => page.GetByTestId("email");

    // ==================== Locators (shared fields) ====================

    public ILocator SubjectSelect => page.GetByTestId("subject");

    public ILocator MessageTextarea => page.GetByTestId("message");

    public ILocator AttachmentInput => page.GetByTestId("attachment");

    public ILocator SubmitButton => page.GetByTestId("contact-submit");

    // ==================== Feedback Locators ====================

    public ILocator SuccessAlert => page.GetByRole(AriaRole.Alert);

    public ILocator FirstNameError => page.GetByTestId("first-name-error");

    public ILocator LastNameError => page.GetByTestId("last-name-error");

    public ILocator EmailError => page.GetByTestId("email-error");

    public ILocator SubjectError => page.GetByTestId("subject-error");

    public ILocator MessageError => page.GetByTestId("message-error");

    public ILocator AttachmentError => page.GetByTestId("attachment-error");

    // Server-side validation errors (e.g. message too long) render in a generic
    // container without a data-test attribute.
    public ILocator ServerError => page.Locator(".alert.alert-danger, .help-block");

    // ==================== Action Methods ====================

    public async Task NavigateToAsync()
    {
        await page.GotoAsync("/contact");
        await SubmitButton.WaitForAsync();
    }

    public async Task FillFirstNameAsync(string value) => await FirstNameInput.FillAsync(value);

    public async Task FillLastNameAsync(string value) => await LastNameInput.FillAsync(value);

    public async Task FillEmailAsync(string value) => await EmailInput.FillAsync(value);

    public async Task SelectSubjectAsync(string subject) =>
        await SubjectSelect.SelectOptionAsync(new SelectOptionValue { Label = subject });

    public async Task FillMessageAsync(string message) => await MessageTextarea.FillAsync(message);

    public async Task AttachFileAsync(string filePath) => await AttachmentInput.SetInputFilesAsync(filePath);

    public async Task ClickSubmitAsync() => await SubmitButton.ClickAsync();

    public async Task SubmitAsync()
    {
        await Task.WhenAll(
            page.WaitForResponseAsync(r =>
                r.Url.Contains("/messages") && r.Request.Method == "POST" && r.Ok),
            SubmitButton.ClickAsync());
    }

    /// <summary>Fills all guest contact form fields without submitting.</summary>
    public async Task FillOutAsGuestAsync(string firstName, string lastName, string email, string subject, string message)
    {
        await FillFirstNameAsync(firstName);
        await FillLastNameAsync(lastName);
        await FillEmailAsync(email);
        await SelectSubjectAsync(subject);
        await FillMessageAsync(message);
    }

    /// <summary>Fills and submits the contact form as a guest user (all fields required).</summary>
    public async Task SubmitAsGuestAsync(string firstName, string lastName, string email, string subject, string message)
    {
        await FillOutAsGuestAsync(firstName, lastName, email, subject, message);
        await SubmitAsync();
    }

    /// <summary>Fills and submits the contact form as a guest user with an attachment.</summary>
    public async Task SubmitAsGuestWithAttachmentAsync(
        string firstName, string lastName, string email, string subject, string message, string filePath)
    {
        await FillOutAsGuestAsync(firstName, lastName, email, subject, message);
        await AttachFileAsync(filePath);
        await SubmitAsync();
    }

    /// <summary>Fills and submits the contact form as a registered user (name/email pre-filled).</summary>
    public async Task SubmitAsRegisteredUserAsync(string subject, string message)
    {
        await SelectSubjectAsync(subject);
        await FillMessageAsync(message);
        await SubmitAsync();
    }
}
