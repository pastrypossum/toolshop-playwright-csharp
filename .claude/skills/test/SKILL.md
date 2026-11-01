---
name: test
description: Conventions for writing test classes/specs in this project: structure, naming, framework annotations, and assertion style. Use before writing or modifying a test.
---

# Test Instructions

## Base class

Every UI test class derives from `Toolshop.Playwright.Tests.Fixtures.ToolshopTest` (in `Fixtures/ToolshopTest.cs`), not from `Microsoft.Playwright.NUnit.PageTest` directly:

```csharp
[AllureNUnit]
[AllureSuite("Contact customer support")]
[TestFixture]
public class ContactCustomerSupportTests : ToolshopTest
{
    private ContactPage _contactForm = null!;

    [SetUp]
    public void SetUp()
    {
        _contactForm = new ContactPage(Page);
    }

    [Test]
    [AllureName("Guest submits a valid contact form and sees an acknowledgement message")]
    public async Task GuestSubmitsValidContactFormAndSeesAcknowledgement()
    {
        await _contactForm.NavigateToAsync();
        // ...
    }
}
```

`ToolshopTest` gives every test:
- A browser context whose `BaseURL` is `BASE_URL` (env var) or `https://practicesoftwaretesting.com` — navigate with relative paths (`Page.GotoAsync("/contact")`), never hardcode the host in a test or page object.
- The app's `data-test` test-id attribute wired up automatically (see `page-object.instructions.md`).

`Page`, `Context`, `Browser`, `Playwright` are inherited protected properties from `Microsoft.Playwright.NUnit.PageTest` — do not re-declare them.

---

## Structure

- One `[TestFixture]` class per scenario group (e.g. `ContactCustomerSupportTests`, `ProductSearchTests`), under `Tests/{Area}/`.
- Page objects are constructed in `[SetUp]`, stored in private fields, and used from every `[Test]` method in the fixture. **Never** call `Page.Locator(...)`/`Page.GetByTestId(...)` directly in a test — go through the page object.
- Structure a test body around Given/When/Then. NUnit itself has no built-in `test.step()` equivalent, but Allure.NUnit does: wrap each clause in `AllureApi.Step("<clause text>", ...)` (see `allure.instructions.md`) rather than a plain comment, so the structure shows up as real steps in the report, not just source commentary. Existing tests predating this (`ContactCustomerSupportTests`, `ProductSearchTests`) use comments instead and are left as-is — not a pattern to copy into new tests.
- Assert with Playwright's auto-retrying `Assertions.Expect(locator)...Async()` for anything UI-state related (visibility, text) — see `Microsoft.Playwright.Assertions`. Use plain NUnit `Assert.That(...)` only for plain C# values (e.g. a list of strings returned from `AllInnerTextsAsync()`), as in `ProductSearchTests`.
- Use a concrete example in test data and assertions (a specific product name, a specific message) rather than vague placeholders — see `ProductSearchTests.SearchingForKeywordReturnsMatchingProducts` searching for the concrete term `"Pliers"`.

## UI-first principle

These are UI tests: they drive the app through the browser like a real user.

- All interactions go through page objects.
- `Fixtures/APIHelper.cs` exists (see `api-helper.instructions.md`) and may be used for setup/teardown/verification that cannot be done through the UI — never to shortcut the feature under test itself.

## Anti-Patterns ❌

- `Page.WaitForTimeoutAsync()` / manual delays.
- Instantiating a page object anywhere except `[SetUp]`.
- Asserting inside a page object instead of the test.
- Hardcoding the live URL (`https://practicesoftwaretesting.com`) instead of relying on `ToolshopTest.BaseUrl` / relative `GotoAsync` calls.
