using Bogus;

namespace Toolshop.Playwright.Tests.Data.Contact;

/// <summary>A fully-populated, valid guest contact message.</summary>
public record ContactMessage(string FirstName, string LastName, string Email, string Subject, string Message);

/// <summary>
/// Generates dynamic contact-form test data with <c>Bogus</c> (the .NET equivalent of
/// Java's DataFaker and TypeScript's <c>@faker-js/faker</c>), mirroring
/// <c>contact-message.factory.ts</c>.
/// </summary>
public static class ContactMessageFactory
{
    private static readonly Faker Faker = new();

    /// <summary>
    /// Builds a valid guest contact message with unique personal details. The email uses a
    /// short random suffix to guarantee uniqueness across test runs when a test needs to look
    /// the message back up (e.g. via an API query by email).
    /// </summary>
    public static ContactMessage BuildGuestContactMessage()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        return new ContactMessage(
            FirstName: Faker.Name.FirstName(),
            LastName: Faker.Name.LastName(),
            Email: $"test+{uniqueId}@example.com",
            Subject: "Customer service",
            Message: Faker.Lorem.Sentences(2).PadRight(55, '.'));
    }
}
