using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.User;

/// <summary>
/// Intentionally failing tests tracking a known error-handling gap in how this resource responds
/// to a <c>{userId}</c> that doesn't exist - found while exploring <c>GET</c>/<c>DELETE
/// /users/{userId}</c> as admin. Unlike Brand/Category's analogous "missing id" case (both
/// return a clean 404 with <c>{"message": "Requested item not found"}</c>, per
/// <c>ItemNotFoundResponse</c>), User's handling of the same scenario is broken two different
/// ways:
///
/// <list type="bullet">
/// <item><c>GET</c> leaks an internal Eloquent exception message instead of the documented
/// <c>ItemNotFoundResponse</c> shape.</item>
/// <item><c>DELETE</c> doesn't catch the equivalent exception at all and returns a raw
/// <c>500</c>, even though the spec documents <c>404</c> for this exact case (and Brand/Category
/// both correctly return 404 for their own missing-id deletes).</item>
/// </list>
///
/// These assert the behavior the spec already documents - this isn't a judgment call like
/// <c>BrandAuthGapSpecTests</c>/<c>CategoryAuthGapSpecTests</c>, it's a real bug against an
/// existing written contract.
///
/// Remove or flip these once the gap is fixed - don't let them sit red indefinitely without
/// someone deciding.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("User")]
[AllureSubSuite("UserMissingUserErrorGapSpecTests")]
public class UserMissingUserErrorGapSpecTests : UserContractTestBase
{
    [Test]
    [AllureName("KNOWN GAP: GET /users/{id} for a missing user should return the documented ItemNotFoundResponse shape, not leak an Eloquent exception message")]
    public async Task GetById_MissingUser_ShouldReturnDocumentedNotFoundShape()
    {
        var token = await GetAdminTokenAsync();

        var response = await ApiContext.GetAsync("/users/does-not-exist", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });

        Assert.That(response.Status, Is.EqualTo(404));
        var body = (await response.JsonAsync())!.Value;
        // The real shape is {"error": "No query results for model [App\\Models\\User] ..."} -
        // no "message" property at all, so GetProperty("message") would throw rather than fail
        // the assertion cleanly. TryGetProperty keeps this test a clean, readable red instead of
        // a crash while the gap stands.
        var hasDocumentedShape = body.TryGetProperty("message", out var message) && message.GetString() == "Requested item not found";
        Assert.That(hasDocumentedShape, Is.True,
            "should match the spec's ItemNotFoundResponse ({\"message\": \"Requested item not found\"}), not leak 'No query results for model [App\\Models\\User] ...' under an \"error\" key");
    }

    [Test]
    [AllureName("KNOWN GAP: DELETE /users/{id} for a missing user should return 404 per the spec, not crash with a 500")]
    public async Task Delete_MissingUser_ShouldReturnNotFoundNotServerError()
    {
        var token = await GetAdminTokenAsync();

        var response = await DeleteUserAsync("does-not-exist", token);

        Assert.That(response.Status, Is.EqualTo(404),
            "the spec documents 404 for DELETE /users/{id} on a missing user, matching Brand's and Category's equivalent deletes - this currently 500s instead");
    }
}
