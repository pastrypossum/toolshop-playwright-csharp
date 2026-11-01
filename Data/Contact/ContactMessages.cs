namespace Toolshop.Playwright.Tests.Data.Contact;

/// <summary>
/// Static, known-fixed strings asserted against in contact-form tests, kept out of the
/// test/page-object files per the test-data convention shared with the TypeScript project
/// (see <c>contact-validation.ts</c>).
/// </summary>
public static class ContactMessages
{
    public const string Success = "Thanks for your message! We will contact you shortly.";
    public const string SubjectRequired = "Subject is required";
    public const string MessageRequired = "Message is required";
    public const string MessageTooShort = "Message must be minimal 50 characters";
    public const string MessageTooLong = "The message field must not be greater than 250 characters.";
    public const string AttachmentInvalidFormat = "File should have a txt extension.";
    public const string AttachmentInvalidSize = "File should be empty.";
}
