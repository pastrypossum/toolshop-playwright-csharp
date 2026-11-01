namespace Toolshop.Playwright.Tests.Data.Auth;

/// <summary>A default seeded account for the toolshop application.</summary>
public record Account(string Name, string Role, string Email, string Password);

/// <summary>
/// Default seeded accounts for the toolshop application, mirroring the TypeScript project's
/// <c>DEFAULT_ACCOUNTS</c> in <c>test-data/auth/accounts.ts</c>. Use these in tests that
/// require a pre-existing user account rather than registering a new one.
/// </summary>
public static class Accounts
{
    public static readonly Account Admin = new("John Doe", "admin", "admin@practicesoftwaretesting.com", "welcome01");
    public static readonly Account Customer = new("Jane Doe", "user", "customer@practicesoftwaretesting.com", "welcome01");
    public static readonly Account Customer2 = new("Jack Howe", "user", "customer2@practicesoftwaretesting.com", "welcome01");
    public static readonly Account Customer3 = new("Bob Smith", "user", "customer3@practicesoftwaretesting.com", "pass123");
}
