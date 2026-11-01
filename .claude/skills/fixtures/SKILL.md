---
name: fixtures
description: Conventions for this project's test base classes in Fixtures/ (ToolshopTest, ApiTest) - what each provides today, and when a cross-cutting concern belongs on one of them versus somewhere else. Use before adding something every (or nearly every) test of a given kind needs.
---

# Fixtures

> DRAFT - piloting against a real feature before this is treated as settled.

## What lives here, today

Two base classes, both in `Fixtures/`:

| Class | Derives from | Every test gets |
| --- | --- | --- |
| `ToolshopTest` | `Microsoft.Playwright.NUnit.PageTest` | A browser `Page` with `BaseURL` set from `BASE_URL`; the `data-test` test-id attribute wired up; UI tests forced fully sequential (`[Parallelizable(ParallelScope.None)]` - see the class's own doc comment for why). |
| `ApiTest` | `Microsoft.Playwright.NUnit.PlaywrightTest` | An `ApiContext` (`IAPIRequestContext`) bound to `API_URL`, created in `[SetUp]` and disposed in `[TearDown]` - no browser at all. |

`APIHelper` (see `api-helper.instructions.md`) is a separate, plain class - not a fixture base class
itself - constructed from whichever `ApiContext` is available.

## The one known gap

`ToolshopTest` doesn't expose an `ApiContext`, so a UI test that wants `APIHelper` for
setup/teardown (per `api-helper.instructions.md`'s actual, sanctioned use cases) has to build one
itself. Add the same `[SetUp]`/`[TearDown]` pair `ApiTest` already has, directly onto `ToolshopTest`:

```csharp
protected IAPIRequestContext ApiContext { get; private set; } = null!;

[SetUp]
public async Task CreateApiContextAsync()
{
    ApiContext = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
    {
        BaseURL = ApiUrl,
    });
}

[TearDown]
public async Task DisposeApiContextAsync()
{
    await ApiContext.DisposeAsync();
}
```

This duplicates `ApiTest`'s own three lines rather than extracting a shared helper or common base -
C# can't multiple-inherit (`ToolshopTest : PageTest` and `ApiTest : PlaywrightTest` are already two
different base classes), and this project's own convention is flat, unabstracted test-base classes;
a handful of duplicated lines is cheaper than a mixin/interface built to avoid them. If a third base
class ever needs the exact same pair, that's the signal to reconsider - not before.

## What belongs on a fixture versus somewhere else

A fixture concern is something **every** test of that base class needs, unconditionally, every run
- not something only some scenarios need:

| This test needs... | Goes in... |
| --- | --- |
| Something every test of this kind needs, every time (a browser context option, an `ApiContext`) | The shared base class (`ToolshopTest`/`ApiTest`) |
| Something only this test file's scenarios need, every test in the file | That file's own `[SetUp]` (a page object, a common action, `APIHelper`) |
| Something only one or two tests in the file need | Built inside the test method itself, not `[SetUp]` |

Don't add scenario-specific setup to a shared base class on the theory that "it might be useful
elsewhere" - that's exactly the promote-on-second-consumer judgement the rest of this project's test
data and factory rules already use, applied to fixtures instead.

## Anti-Patterns ❌

- Adding a scenario-specific concern (one feature's page objects, one feature's common actions) to
  `ToolshopTest`/`ApiTest` directly, instead of to the consuming test file's own `[SetUp]`.
- Re-implementing something Playwright's own `PageTest`/`PlaywrightTest` already provides
  (`Page`, `Context`, `Browser`, `Playwright` are inherited - never re-declare them).
- Building a shared mixin/interface to avoid the `ApiContext` duplication above before a third base
  class actually needs it.
