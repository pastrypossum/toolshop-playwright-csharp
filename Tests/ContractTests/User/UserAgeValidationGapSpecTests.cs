using Allure.Net.Commons.Attributes;
using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Tests.ContractTests.User;

/// <summary>
/// Intentionally failing test tracking a known validation gap found while binary-searching
/// <c>POST /users/register</c>'s upper age boundary: the error message says
/// <c>"Customer must be younger than 75 years old."</c> (see
/// <see cref="UserSpecTests.Register_TooOld_ReturnsUnprocessableEntity"/>), and the OpenAPI spec
/// documents the same "between 18 and 75 years" rule - but the rule actually enforced rejects
/// only at 93 years old or older. A dob giving an age of 76 through 92 is accepted with 201, a
/// full 18 extra years past what both the spec and the error message itself claim. (18 is
/// suspicious: it's exactly the minimum-age requirement, suggesting the max-age check is
/// comparing against the wrong reference date somewhere - but this test only asserts the
/// observable boundary, not the cause.)
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
[AllureSubSuite("UserAgeValidationGapSpecTests")]
public class UserAgeValidationGapSpecTests : UserContractTestBase
{
    [Test]
    [AllureName("KNOWN GAP: POST /users/register with a dob giving age 80 (over the documented 75-year ceiling) should return 422, but currently returns 201")]
    public async Task Register_OlderThan75ButUnder93_ShouldBeRejectedButIsNot()
    {
        var response = await ApiContext.PostAsync("/users/register", new APIRequestContextOptions
        {
            DataObject = new
            {
                first_name = "Age Gap",
                last_name = "Probe",
                email = $"age-gap-probe-{Guid.NewGuid():N}@example.com",
                password = GenerateStrongPassword(),
                dob = DateTime.UtcNow.AddYears(-80).ToString("yyyy-MM-dd"),
            },
        });

        if (response.Status == 201)
        {
            // The gap: it was created despite being 5 years over the documented ceiling. Clean
            // up so this test doesn't leak an account on every run while it's still red.
            var createdBody = (await response.JsonAsync())!.Value;
            CreatedUserId = createdBody.GetProperty("id").GetString();
        }

        Assert.That(response.Status, Is.EqualTo(422),
            "age 80 should fail the documented 'must be younger than 75 years old' rule - currently the real ceiling is 93, not 75");
    }
}
