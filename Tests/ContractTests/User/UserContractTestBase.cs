using Microsoft.Playwright;
using NUnit.Framework;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Fixtures;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.User;

/// <summary>A throwaway registered customer created for one test, with the credentials needed to log back in as it.</summary>
public record ThrowawayUser(string Id, string Email, string Password);

/// <summary>
/// Shared setup/teardown for user contract tests: admin login, throwaway-user registration, and
/// automatic cleanup via <see cref="CreatedUserId"/>. Mirrors
/// <c>Tests/ContractTests/Brand/BrandContractTestBase.cs</c>.
///
/// Every destructive or state-changing test in this resource (register, login, change-password,
/// forgot-password, delete, update) must operate on a throwaway user created here, never on the
/// shared seeded accounts in <see cref="Accounts"/> - this project's whole suite, across all
/// three sibling language projects, logs in as <see cref="Accounts.Admin"/> and
/// <see cref="Accounts.Customer"/> against the same live demo environment, so changing their
/// password or deleting them would break every other test that runs concurrently.
/// </summary>
public abstract class UserContractTestBase : ApiTest
{
    /// <summary>
    /// Set this from a test (or a derived [SetUp]) when a throwaway user was created and needs
    /// deleting afterward. Left null, nothing runs. For a test that creates more than one user,
    /// delete the extras yourself and leave only the last one for this field (see
    /// <c>UserSpecTests</c>'s ownership-boundary tests for the pattern).
    /// </summary>
    protected string? CreatedUserId;

    protected Task<string> GetAdminTokenAsync() => new APIHelper(ApiContext).LoginAsync(Accounts.Admin);

    protected Task<string> LoginAsAsync(string email, string password) =>
        new APIHelper(ApiContext).LoginAsync(new Account("Throwaway", "user", email, password));

    /// <summary>
    /// A password satisfying the documented policy (upper/lower/digit/symbol, 8+ chars) that is
    /// different every call - a fixed literal risks tripping the live breach-database check
    /// (<c>Create_LeakedPassword_ReturnsUnprocessableEntity</c> exists specifically because a
    /// hardcoded "strong" password like the spec's own example, <c>SuperSecure@123</c>, is itself
    /// a known-leaked one).
    /// </summary>
    protected static string GenerateStrongPassword() => $"Qx7#{Guid.NewGuid():N}zK9!";

    protected async Task<ThrowawayUser> RegisterThrowawayUserAsync(string namePrefix, string? password = null)
    {
        password ??= GenerateStrongPassword();
        var email = $"{namePrefix.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}@example.com";

        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = namePrefix, last_name = "Probe", email, password },
        });
        var id = (await response.JsonAsync())!.Value.GetProperty("id").GetString()!;
        return new ThrowawayUser(id, email, password);
    }

    protected Task<IAPIResponse> DeleteUserAsync(string userId, string bearerToken) =>
        ApiContext.DeleteAsync($"/users/{userId}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {bearerToken}" },
        });

    [TearDown]
    public async Task DeleteCreatedUserAsync()
    {
        if (CreatedUserId is null)
        {
            return;
        }

        var token = await GetAdminTokenAsync();
        await DeleteUserAsync(CreatedUserId, token);
    }
}
