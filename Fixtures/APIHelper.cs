using System.Text.Json;
using Microsoft.Playwright;
using Toolshop.Playwright.Tests.Data.Auth;
using Toolshop.Playwright.Tests.Utilities;

namespace Toolshop.Playwright.Tests.Fixtures;

/// <summary>
/// The payload <c>POST /users/register</c> actually needs. Only <c>first_name</c>, <c>last_name</c>,
/// <c>email</c> and <c>password</c> are validated as <c>required</c> by the API's own
/// <c>StoreCustomer</c> request (confirmed by reading it directly, not assumed) - <c>dob</c> is
/// included because it costs nothing and is cheap insurance against business logic that might read
/// it; <c>phone</c>/<c>address</c> are left out because nothing Authentication tests use needs them.
/// </summary>
public record CustomerRegistration(string FirstName, string LastName, string Dob, string Email, string Password);

/// <summary>
/// Cross-cutting API setup support - mirrors Java's <c>fixtures/APIAuth.java</c>. Add methods
/// here as real tests need them; don't split it into a per-resource layer until it's unwieldy.
/// </summary>
public class APIHelper(IAPIRequestContext request)
{
    /// <summary>Logs in as the given account and returns the bearer access token.</summary>
    public async Task<string> LoginAsync(Account account)
    {
        var response = await request.PostAsync("/users/login", new APIRequestContextOptions
        {
            DataObject = new { email = account.Email, password = account.Password },
        });

        if (response.Status != 200)
        {
            throw new InvalidOperationException($"Login as '{account.Email}' failed with status {response.Status}: {await response.TextAsync()}");
        }

        var body = await response.JsonAsync();
        return body!.Value.GetProperty("access_token").GetString()!;
    }

    /// <summary>Registers a new customer via the API and returns the created user's id.</summary>
    public async Task<string> RegisterCustomerAsync(CustomerRegistration registration)
    {
        var response = await request.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new
            {
                first_name = registration.FirstName,
                last_name = registration.LastName,
                dob = registration.Dob,
                email = registration.Email,
                password = registration.Password,
            },
        });

        if (response.Status != 201)
        {
            throw new InvalidOperationException($"Registering '{registration.Email}' failed with status {response.Status}: {await response.TextAsync()}");
        }

        var body = await response.JsonAsync();
        return body!.Value.GetProperty("id").ToString();
    }

    /// <summary>
    /// Logs in as the given (not-yet-TOTP-enabled) account, sets up and verifies TOTP for it via
    /// <c>POST /totp/setup</c> + <c>POST /totp/verify</c>, and returns the TOTP secret so the
    /// caller can compute fresh codes for its own later login attempts.
    /// </summary>
    public async Task<string> EnableTotpAsync(string email, string password)
    {
        var accessToken = await LoginAsync(new Account(Name: "", Role: "user", email, password));
        var authHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {accessToken}" };

        var setupResponse = await request.PostAsync("/totp/setup", new APIRequestContextOptions { Headers = authHeaders });
        if (setupResponse.Status != 200)
        {
            throw new InvalidOperationException($"TOTP setup for '{email}' failed with status {setupResponse.Status}: {await setupResponse.TextAsync()}");
        }

        var secret = (await setupResponse.JsonAsync())!.Value.GetProperty("secret").GetString()!;
        var code = TotpCodeGenerator.ComputeCurrentCode(secret);

        var verifyResponse = await request.PostAsync("/totp/verify", new APIRequestContextOptions
        {
            Headers = authHeaders,
            DataObject = new { totp = code },
        });
        if (verifyResponse.Status != 200)
        {
            throw new InvalidOperationException($"TOTP verify for '{email}' failed with status {verifyResponse.Status}: {await verifyResponse.TextAsync()}");
        }

        return secret;
    }

    /// <summary>Disables the given user (by id) using the shared admin account's authority.</summary>
    public async Task DisableAccountAsync(string id)
    {
        var adminToken = await LoginAsync(Accounts.Admin);
        var response = await request.PatchAsync($"/users/{id}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" },
            DataObject = new { enabled = false },
        });

        if (response.Status != 200)
        {
            throw new InvalidOperationException($"Disabling account '{id}' failed with status {response.Status}: {await response.TextAsync()}");
        }
    }

    /// <summary>Deletes the given user (by id) using the shared admin account's authority.</summary>
    public async Task DeleteAccountAsync(string id)
    {
        var adminToken = await LoginAsync(Accounts.Admin);
        var response = await request.DeleteAsync($"/users/{id}", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {adminToken}" },
        });

        if (response.Status != 204)
        {
            throw new InvalidOperationException($"Deleting account '{id}' failed with status {response.Status}: {await response.TextAsync()}");
        }
    }
}
