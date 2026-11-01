using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.User;

/// <summary>
/// Intentionally failing test tracking a known validation gap found while exploring
/// <c>POST /users/register</c>: the <c>UserRequest</c> schema declares <c>email</c> as
/// <c>format: email</c>, and every other email field in this request/response pair is treated as
/// one (it's what <c>/users/login</c>, <c>/users/forgot-password</c>, and the duplicate-email
/// 409 check all key off), but the registration endpoint itself never actually validates the
/// format - a syntactically invalid string is accepted and stored as a real account's email.
///
/// This is a real correctness bug against the documented schema, not a judgment call like
/// <c>BrandAuthGapSpecTests</c>/<c>CategoryAuthGapSpecTests</c> - asserted here as a red test
/// rather than silently worked around.
///
/// Remove or flip this once the gap is fixed - don't let it sit red indefinitely without someone
/// deciding.
/// </summary>
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("User")]
[AllureSubSuite("UserRegistrationValidationGapSpecTests")]
public class UserRegistrationValidationGapSpecTests : UserContractTestBase
{
    [Test]
    [AllureName("KNOWN GAP: POST /users/register with a syntactically invalid email should return 422 per the documented format:email, not 201")]
    public async Task Register_InvalidEmailFormat_ShouldBeRejected()
    {
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new { first_name = "Gap", last_name = "Probe", email = "not-an-email", password = GenerateStrongPassword() },
        });

        if (response.Status == 201)
        {
            // The gap: it was created despite the invalid email. Clean up so this test doesn't
            // leak an unreachable account on every run while it's still red.
            var createdBody = (await response.JsonAsync())!.Value;
            CreatedUserId = createdBody.GetProperty("id").GetString();
        }

        Assert.That(response.Status, Is.EqualTo(422), "'not-an-email' should fail UserRequest's documented format:email - currently it does not");
    }
}
