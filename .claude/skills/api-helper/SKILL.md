---
name: api-helper
description: When (and when not) to add an API helper for test setup/teardown/verification, and where it belongs in this project. Use before adding any direct API call inside a test.
---

# API Helper Instructions

## Current status

`Fixtures/APIHelper.cs` exists today — a single `APIHelper(IAPIRequestContext request)` class with
one real method, `LoginAsync(Account account)`. Contract-test base classes already use it (see
`contract-testing.instructions.md`). UI tests under `Tests/AcceptanceTests/` have historically driven
the application purely through the UI via Playwright's `IPage`, but that's a gap being closed, not a
rule — `ToolshopTest` doesn't yet expose an `ApiContext` the way `Fixtures/ApiTest.cs` does for API
tests, so a UI test that wants `APIHelper` for setup/teardown (see "Purpose" below) constructs its own
context today; adding `ApiContext` to `ToolshopTest` itself is still open work.

Add new methods directly to this one class as real tests need them (see "File Location" below) —
don't reach for `HttpClient` or `IPage.APIRequest` ad hoc in a test, and don't start a second helper
class.

The TypeScript sibling project has a mature `helpers/api/*.api.ts` layer (`MessagesApi`,
`ProductsApi`, `BrandsApi`, ...) built on Playwright's `APIRequestContext`; this project's single
`APIHelper` is the straightforward C# port of that convention using
`Microsoft.Playwright.IAPIRequestContext`, which every `Microsoft.Playwright.NUnit.PlaywrightTest`-
derived class (and therefore every `ToolshopTest`) can obtain via
`Playwright.APIRequest.NewContextAsync(...)`.

## Purpose

API helpers support UI tests — they are **not** a replacement for the UI. Use them only when a
test has a genuine need that cannot be met through the browser:

| Use case | Example |
| --- | --- |
| **Setup** — create prerequisite data for a feature that is *not under test* | Creating a product so a cart test has something to add |
| **Teardown** — clean up data the UI has no delete flow for | Removing a message created during a journey test |
| **Verification** — assert a side-effect not visible in the UI | Confirming a submitted contact message landed in the support queue with the right category |

**Do not** use an API helper to set up or tear down data for the feature being tested. If the
scenario under test is "guest submits the contact form", submit it through `ContactPage` — that
is what is being tested.

## File Location

```
Fixtures/APIHelper.cs
```

A single `APIHelper` class alongside `ToolshopTest` in `Fixtures/` — mirroring Java's
`fixtures/APIAuth.java` — not a `{Name}Api.cs`-per-resource layer like the TypeScript
project's `helpers/api/`. Add methods to it as real tests need them; don't split it up
until it's actually unwieldy.

## Pattern

The real file today:

```csharp
using Microsoft.Playwright;

namespace Toolshop.Playwright.Tests.Fixtures;

public class APIHelper(IAPIRequestContext request)
{
    public async Task LoginAsync(Account account) { /* ... */ }
}
```

A method added for a new use case follows the same shape — instance method on the one class, taking
whatever it needs and returning what the caller requires:

```csharp
public async Task<JsonElement> GetLatestGuestMessageByEmailAsync(string email)
{
    var response = await request.GetAsync($"/messages?email={Uri.EscapeDataString(email)}");
    var body = await response.JsonAsync();
    return body!.Value.GetProperty("data")[0];
}
```

```csharp
// In a test, construct the request context once (e.g. in [SetUp])
var apiContext = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
{
    BaseURL = ApiUrl, // ToolshopTest.ApiUrl, backed by the API_URL env var
});
var apiHelper = new APIHelper(apiContext);
```

## Anti-Patterns ❌

- Using an API helper to set up or tear down data for the feature under test — use the UI instead.
- Calling an API helper inside a "Given" step when the feature being tested is the create/submit
  flow itself.
- Reaching for `System.Net.Http.HttpClient` instead of Playwright's own `IAPIRequestContext` —
  the latter shares cookies/auth state with the browser context when needed and keeps the
  dependency surface the same as the UI layer.
