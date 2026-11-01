---
name: common-actions
description: When and how to extract a composite action - one that calls more than one page object and/or API helper - into its own reusable class, as part of Stage 3 (Supporting code) of the existing-feature-acceptance-testing workflow. Use before writing a Given/When step whose setup spans multiple page objects or an API helper plus a page object.
---

# Common Actions

> DRAFT - piloting against a real feature before this is treated as settled.

## What this is

A common action composes **more than one** page-object and/or API-helper call into a single,
reusable, higher-level step - something a page object can't own (it only has an `IPage`) and
`APIHelper` can't own (it only has an `IAPIRequestContext`). It exists one level above both:
something that needs *both*, or needs several calls across either, coordinated together.

Concrete example: "register a throwaway customer, then enable TOTP for them" needs
`APIHelper.RegisterCustomerAsync(...)`, `APIHelper.EnableTotpAsync(...)`, and (to get the account
into a state the test can continue from) `LoginPage.NavigateToAsync()`. No single page object or
the API helper alone can express that - it needs a class that holds both.

## When to extract one

Default to writing the composite call inline in the test's own `AllureApi.Step` block (as
`LoginTests.cs` originally did). Extract it into its own class once **either**:

- A second test needs the exact same composite action (the same promote-on-second-consumer rule
  `test-data.instructions.md` uses for factories/datasets), or
- Even for one consumer, the step's own body is genuinely complex enough to hurt that test's
  readability on its own - coordinating three or more calls, or crossing what would otherwise be
  two different features' supporting code (the TOTP example above touches both Registration and
  Authentication). This is a judgement call, not a count to hit.

A single page-object action method that happens to make two Playwright calls (`FillAsync` then
`ClickAsync`) is not a common action - that's just an ordinary action method, see
`page-object.instructions.md`. A common action specifically crosses the page-object/API-helper
boundary, or coordinates across more than one of either.

## Structure

One class per test **Area** (the folder under `Tests/AcceptanceTests/{Area}/` that consumes it),
not per Feature and not one shared class for the whole project - see
`existing-feature-acceptance-testing.instructions.md` for why (the consumer owns it, so Login's
Stage 3 isn't blocked on Registration's own Stage 3 existing first).

```
Actions/{Area}Actions.cs
```

Takes whatever page objects and/or `APIHelper` it needs via a primary constructor, exactly like a
page object takes `IPage` - constructed once in the test's `[SetUp]`, alongside the page objects and
API helper it wraps, and stored in its own field:

```csharp
// Actions/AuthenticationActions.cs
using Toolshop.Playwright.Tests.Fixtures;
using Toolshop.Playwright.Tests.Pages;

namespace Toolshop.Playwright.Tests.Actions;

public class AuthenticationActions(LoginPage loginPage, APIHelper apiHelper)
{
    /// <summary>
    /// Registers the given throwaway customer and enables TOTP for them, leaving the browser on
    /// the login page ready to sign in. Returns the TOTP secret - the one piece of data the test
    /// couldn't have supplied itself, since the API generates it.
    /// </summary>
    public async Task<string> RegisterAndEnableTotpAsync(Registration registration)
    {
        await apiHelper.RegisterCustomerAsync(registration);
        var totpSecret = await apiHelper.EnableTotpAsync(registration.Email, registration.Password);
        Assert.That(totpSecret, Is.Not.Empty, "Setup failed: enabling TOTP did not return a secret");

        await loginPage.NavigateToAsync();

        return totpSecret;
    }
}
```

```csharp
// In the test's [SetUp], alongside _loginPage and _apiHelper
_authActions = new AuthenticationActions(_loginPage, _apiHelper);
```

## Data stays owned by the test

The test decides what data it needs - built inline, from a factory, or from a shared fixture, per
`test-data.instructions.md` - and hands it to the action. A common action never builds its own
default data; it executes with whatever the test gives it. It may return data the test *couldn't*
have supplied (a generated TOTP secret, a server-assigned id) or a derived subset of what it was
given - never an unprompted echo of the input the caller already has.

## Assertions: postcondition guards, not the test's proof

A common action may assert that its own effect actually happened - e.g. the registration call
returned a usable id - so a broken setup fails loudly and immediately, at the point of failure,
rather than silently cascading into a confusing failure three steps later in the test's own Then
clause. This is a **guard**, not the AC's confirming assertion. NUnit can't tell the two apart
mechanically (either one just throws if false) - the difference is purely what it's *for*:

- A common action's internal assertion only confirms its own precondition/postcondition held.
- The test's own Then step must still independently assert the tangible, user-visible outcome, per
  the assertion rule in `writing-acceptance-tests.instructions.md` - even where that looks
  redundant with a guard the action already checked.

Never let a common action's guard assertion be the only assertion standing between the test and a
false pass.

## Allure steps

A common action's methods may carry `[AllureStep]` (from `Allure.Net.Commons.Attributes`, see
`allure.instructions.md`) the same way a page-object action method does - each call then nests as
its own step under whichever `AllureApi.Step` is open in the calling test, so the composite action's
internal structure is still visible in the report, not collapsed into one opaque step.

## Anti-Patterns ❌

- Putting a test-outcome assertion inside a common action - that's the test method's job.
- A common action building its own default/hardcoded test data instead of taking it as a parameter.
- One shared `Actions/CommonActions.cs` for the whole project, or splitting by Feature instead of by
  the consuming test Area.
- Extracting a composite action the first time it's needed by exactly one test, when inlining it in
  that one `AllureApi.Step` block would have been just as readable.
