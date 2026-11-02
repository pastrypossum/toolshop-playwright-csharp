---
name: page-object
description: Conventions for creating or updating Playwright page objects in this project: locators, interaction/action methods, locator priority, auto-waiting, and the no-assertions rule. Use before writing or modifying a page object.
---

# Page Object Instructions

## Critical
- Page objects are named after the page/component they represent, for example: `ContactPage`, `NavBar`, `ProductListPage`.
- A page object is a logical grouping of UI components, not necessarily a physical page of the application.
- Page objects contain: **locators**, **interaction methods**, and **action methods**.
  - **Locators** are `get`-only properties returning `ILocator` — one per interactive element or feedback element (e.g. `EmailInput`, `SuccessAlert`).
  - **Interaction methods** return an `ILocator` for a parameterised UI component (e.g. `GetProductCard(string name)`). Use these when a locator needs a runtime parameter.
  - **Action methods** are `async Task` methods (named `XxxAsync`) that perform a user action scoped entirely to this page (e.g. `SubmitAsGuestAsync(...)`, `SearchForAsync(string query)`).
- **NEVER** include assertions (`Assertions.Expect`/`Assert.That`) inside page objects — only tests assert.
- Every page object with a distinct URL route has a `NavigateToAsync()` action method that calls `page.GotoAsync(...)` for that route. This is the only action method permitted to navigate. Other action methods must not navigate away from the page.
- Page objects take the `IPage` via a primary constructor (`public class ContactPage(IPage page)`) and are instantiated directly in test `[SetUp]` — there is no fixture-injection framework in NUnit the way Playwright's TS `test.extend()` provides it, so `new ContactPage(Page)` in `[SetUp]` is the idiomatic equivalent. Tests must not reach past the page object into `Page.Locator(...)` directly for anything the page object already exposes.

---

## Locator Priority - MANDATORY

This section is not optional guidance - every locator added to this project, in any page object,
follows it. Checking "did I try Role/Text/Label first" is not a nice-to-have; skipping straight to
`GetByTestId` out of habit is a Stage 4 finding every time it's caught, and it has been caught more
than once already (see the real cases below).

## Never guess a locator - find it

Every locator in this section is found by actually looking at the real thing first, never guessed
from the element's name, from memory, or from what seems likely:

- Read the real rendered markup for the element - the Angular component's own `.html` template in
  the application's source, or the live DOM via the browser/Playwright inspector. This gives you the
  real role-mapping-relevant attributes (`type`, `aria-label`, `role`, whether there's a
  `<label for="...">`), not an assumption about what they probably are.
- If the visible text or accessible name comes from a translation key (this app uses Transloco,
  `{{ t('...') }}`), resolve the **real string** from `assets/i18n/en.json` (or whichever locale the
  tests run against) - don't guess the English wording from the key name. A key like
  `fields.email.label` is not reliably "Email" - it might render "Email address", with an extra
  character appended outside the translation (see the real `*` required-marker case below).
- Treat a `data-test` value the same way - read it from the template, don't assume it matches the
  element's purpose or name.

Guessing is far more dangerous for Role/Text/Label than it was for TestId alone: a wrong `data-test`
guess fails immediately and obviously, but a wrong accessible-name guess can silently match the
*wrong* element (or several), or pass today and break the moment the translation copy changes.

## Locator Priority

1. `Page.GetByRole(...)` — preferred wherever the element has a real, stable accessible role and
   name (a button's `aria-label`/text, a link's text, a heading). Prefer this even when a
   `data-test` attribute also exists.
2. `Page.GetByText(...)` — for content with no interactive role, where the visible text itself is
   stable and not also the specific value the test is about to assert (see the anti-pattern below).
3. `Page.GetByLabel(...)` — for form fields whose own type has no mapped ARIA role (`type="password"`
   has none, so it can't be found via `GetByRole`), but there's still a real `<label for="...">`.
4. `Page.GetByTestId(...)` — once Role/Text/Label have been genuinely ruled out, not skipped. The
   app renders a `data-test` on virtually every interactive element (works because
   `ToolshopTest`'s `[SetUp]` calls `Playwright.Selectors.SetTestIdAttribute("data-test")` —
   Playwright's .NET default is `data-testid`, which this app doesn't use), which makes it tempting
   to reach for first; don't. Real cases where it's still the right call: the element's visible
   text/name is dynamic and not known at locator-definition time (e.g. `NavBar.AccountMenuButton`
   shows whichever account's name is currently signed in - no fixed `Name` to query by), or a role
   exists but isn't unique enough on its own to disambiguate (several same-role, same-name controls
   on the page at once).
5. CSS selector via `Page.Locator(...)` — only for elements with no test id and no useful
   role/label/text (e.g. `ContactPage.ServerError` uses `.alert.alert-danger, .help-block` because
   server-side validation errors don't carry a `data-test` either).
6. XPath — last resort only; add a comment explaining why. None of the current page objects need it.

Accessible names drawn from this app's own translation strings (Transloco, `assets/i18n/en.json`)
are still real, stable English text at the project's default test locale - using them isn't
guessing, but it does mean a locator built this way only matches that locale. If a locale switch is
ever genuinely in scope for a test, that's the case to fall back to `GetByTestId` for, not before.

**An `<a>` with no `href` has no implicit link role.** Found for real on this project's nav bar:
a sign-out anchor using `(click)="logout()"` with no `href` at all (it performs an action, not a
navigation) never matches `GetByRole(AriaRole.Link, ...)` - it times out, it doesn't just not match
cleanly. Its sibling sign-in link uses Angular's `routerLink` instead, which *does* render a real
`href`, so it gets role `link` as expected. Confirm which case you actually have by reading the
template's attributes, not by assuming every `<a>` is a link - when there's no `href`, fall through
to `GetByText` instead (role genuinely doesn't apply, this isn't a "just use TestId" case).

**A substring match can hit more than the element you meant.** `GetByRole`/`GetByLabel`/`GetByText`
default to a case-insensitive substring match, which also checks `aria-label` as a fallback
accessible-name source - not just an element's own `<label>`. Found for real on this project's
login page: `GetByLabel("Password")` matched both the real password field *and* the "Forgot your
Password?" link, because the link's `aria-label` contains the substring "Password" too. When this
happens, don't fall back to TestId - fix the match with `new() { Exact = true }` against the real,
full accessible name (confirmed from the template, not guessed).

**Anti-pattern: don't let the locator and the assertion prove the same string.** If a value is
exactly what the test is about to assert (`GetByText("Sales over the years")` immediately followed
by `.ToHaveTextAsync("Sales over the years")`), the assertion can't fail - it's vacuous. Locate by
role alone (`GetByRole(AriaRole.Heading, new() { Level = 1 })`, no `Name` filter) and let the test's
own assertion be the one place the specific expected text appears.

**A `role="alert"` on a conditionally-rendered error `<div>` doesn't make `GetByRole` the right
call.** Found for real on the Registration form: every per-field error region has an explicit
`role="alert"`, but several can be attached at once (submitting a mostly-empty form shows many
fields' errors together) with no distinguishing accessible name between them - Role exists but can't
disambiguate, so TestId is still correct here. Don't write "no mapped role" in a comment without
actually checking - say what the real reason is (ambiguity, not absence).

**Habit is the real risk, not any single hard case.** The first full pass at `RegisterPage` (a
12-field form) reached for `GetByTestId` on every plain text input by default, despite this same
priority order having just been established - not because any of them were genuinely hard cases,
every one of them had a real `<label>` and a mapped role once actually checked. It was caught in a
Stage 4 review, not before. Reaching for TestId by default is the thing this section exists to
prevent; treat every new locator as a case to justify, not a default to fall back to.

---

## File Locations

| Type | Directory | Naming |
| --- | --- | --- |
| Page objects | `Pages/` | `{Name}.cs`, class `{Name}` |

Real examples in this project: `Pages/NavBar.cs`, `Pages/ProductListPage.cs`, `Pages/ContactPage.cs`.

---

## Auto-Waiting

- **Never** use `Page.WaitForTimeoutAsync()`.
- Prefer Playwright's auto-retrying assertions in tests (`Assertions.Expect(locator).ToBeVisibleAsync()`, etc.) to drive waits.
- Use `Page.WaitForResponseAsync(...)` inside action methods when a network call gates the next step — see `ContactPage.SubmitAsync()`, which waits for the `POST /messages` response, and `ProductListPage.SearchForAsync()`, which waits for the `/products` search response.

---

## Page Object Pattern

```csharp
using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Pages;

public class ExamplePage(IPage page)
{
    // ==================== Locators ====================

    public ILocator EmailInput => page.GetByRole(AriaRole.Textbox, new() { Name = "Email" });

    public ILocator PasswordInput => page.GetByLabel("Password"); // type="password" has no mapped role

    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Submit" });

    // ==================== Feedback Locators ====================

    public ILocator SuccessAlert => page.GetByTestId("contact-success"); // dynamic/no stable role+name

    // ==================== Interaction Methods ====================

    public ILocator GetProductCard(string name) =>
        page.Locator("a[data-test^='product-']").Filter(new LocatorFilterOptions { HasText = name });

    // ==================== Action Methods ====================

    public async Task NavigateToAsync() => await page.GotoAsync("/example");

    public async Task SubmitAsync(string email)
    {
        await EmailInput.FillAsync(email);
        await SubmitButton.ClickAsync();
        await page.WaitForResponseAsync(r => r.Url.Contains("/api/submit"));
    }
}
```

This is the exact shape used by `Pages/ContactPage.cs`, `Pages/NavBar.cs` and `Pages/ProductListPage.cs` — read those for real, non-trivial examples (file upload, `SelectOptionAsync`, response-gated submits).
