---
name: allure
description: Conventions for Allure reporting in this project: required annotations/attributes and how to view results. Use before adding or reviewing Allure metadata on a test.
---

# Allure Reporting Instructions

## Setup

This project uses the `Allure.NUnit` + `Allure.Net.Commons` NuGet packages (not `allure-junit5` like the Java project, not `allure-playwright`/`allure-js-commons` like the TypeScript project — the .NET adapter has its own attribute-based API). Configuration lives in `allureConfig.json` at the project root, copied to the output directory by the `.csproj`:

```json
{
  "allure": {
    "directory": "allure-results"
  }
}
```

Results are written to `allure-results/` next to the test binaries (i.e. `bin/Debug/net10.0/allure-results/`), mirroring where the Java (`target/allure-results`) and TypeScript (`allure-results/`) projects put theirs.

## Critical import rule

As of `Allure.NUnit` 2.15.0, the attributes live in **`Allure.Net.Commons.Attributes`**, not `Allure.NUnit.Attributes` (that namespace still exists for backward compatibility but is deprecated upstream). Only `[AllureNUnit]` itself comes from the `Allure.NUnit` namespace:

```csharp
using Allure.Net.Commons.Attributes; // AllureSuite, AllureName, AllureFeature, AllureStory, ...
using Allure.NUnit;                  // AllureNUnit
```

## Allure has two separate hierarchies — pick one per test kind, don't mix

Allure's report has two independent, parallel grouping trees. Each caps at 3 named levels, with the tests themselves as the leaves underneath. They are **not** parent/child of each other — a test lives in one place in the Suites tree and an unrelated place in the Behaviors tree:

| Tab | Levels (top → bottom) | Attributes |
| --- | --- | --- |
| **Suites** | ParentSuite → Suite → SubSuite → *(tests)* | `[AllureParentSuite]` `[AllureSuite]` `[AllureSubSuite]` |
| **Behaviors** | Epic → Feature → Story → *(tests)* | `[AllureEpic]` `[AllureFeature]` `[AllureStory]` |

This project deliberately uses a **different hierarchy per test kind**, both driven by the `Tests/` folder structure — contract testing and E2E acceptance testing are different *types* of testing (verifying an API's contract vs. verifying user-facing behavior), so they're kept on separate axes in the report rather than sharing one taxonomy.

**The general rule for a new top-level `Tests/` folder, not just these two:**

- **Technical / lower-level** test sets (testing a mechanism — an API contract, a schema, a protocol — not tied to a specific user-facing requirement) → **Suites** tab: `[AllureSuite]` / `[AllureSubSuite]`.
- **Requirement-based / higher-level** test sets (testing behavior traceable to an acceptance criterion or user story) → **Behaviors** tab: `[AllureEpic]` / `[AllureFeature]` / `[AllureStory]`.

When adding a new `Tests/{X}/` folder, ask which of those two descriptions fits before picking attributes — don't default to copying whichever pair the nearest existing fixture happens to use.

| Test kind | Folder | Hierarchy used | Mapping |
| --- | --- | --- | --- |
| Contract tests (`Tests/ContractTests/...`) — technical/lower-level | `Tests/ContractTests/{Resource}/{File}.cs` | **Suites** tab | `Tests/{X}/` → `[AllureParentSuite("X")]`; `Tests/{X}/{Y}/` → `[AllureSuite("Y")]`; the file/class itself → `[AllureSubSuite("{ClassName}")]`. |
| Acceptance tests (`Tests/AcceptanceTests/...`) — requirement-based/higher-level | `Tests/AcceptanceTests/{Y}/` | **Behaviors** tab | `Tests/{X}/` → `[AllureEpic("X")]`; `Tests/{X}/{Y}/` → `[AllureFeature("Y")]`. `Story` is the file/class level, same idea as `SubSuite` above — not used yet because every feature here still has exactly one file; add it the moment a feature folder gets a second file. |

Why the file/class gets its own level: each file is also NUnit's unit of parallel scheduling (`[assembly: Parallelizable(ParallelScope.Fixtures)]` in `AssemblyInfo.cs` — a fixture runs on one worker, start to finish, sequentially within itself). Giving it its own report level makes the report's grouping match how the tests actually execute, not just how they're organized on disk.

So: `Tests/ContractTests/Brand/BrandSpecTests.cs` → `[AllureParentSuite("ContractTests")]` + `[AllureSuite("Brand")]` + `[AllureSubSuite("BrandSpecTests")]` (shows up under **Suites**, nested three levels deep). `Tests/AcceptanceTests/Contact/ContactCustomerSupportTests.cs` → `[AllureEpic("AcceptanceTests")]` + `[AllureFeature("Contact customer support")]` (shows up under **Behaviors**; would add `[AllureStory("ContactCustomerSupportTests")]` if `Contact/` ever gets a second file).

**Never mix the two hierarchies on one fixture** (e.g. don't pair `[AllureSuite]` with `[AllureFeature]` on the same class) — pick the hierarchy for the test kind per the table above and use only its attributes. A new `Tests/ContractTests/Product/ProductSpecTests.cs` fixture gets `[AllureParentSuite("ContractTests")]` + `[AllureSuite("Product")]` + `[AllureSubSuite("ProductSpecTests")]`, matching its sibling, not `[AllureFeature]`.

## Allure reports, NUnit runs — two separate systems

Allure's attributes only populate the Allure report; they are invisible to `dotnet test --filter`,
which works against NUnit/VSTest's own trait system instead (verified against the installed
`Allure.Net.Commons`/`Allure.NUnit` package docs — no `IApplyToTest`/`PropertyAttribute`/
`TestCategory` hook anywhere in either). If a fixture's Allure grouping (`[AllureFeature]`, etc.)
should also be runnable via `dotnet test --filter`, add NUnit's own `[Category("...")]` alongside
it — e.g. `dotnet test --filter "TestCategory=Authentication"` needs `[Category("Authentication")]`
on the fixture, a separate attribute from `[AllureFeature("Authentication")]`, not a replacement for
it. The two serve different consumers (the report vs. the test runner) and are added together, not
one in place of the other.

## Labels — apply to every test fixture

| Attribute | Where | Purpose |
| --- | --- | --- |
| `[AllureNUnit]` | On the `[TestFixture]` class | **Mandatory.** Without it, the fixture is not hooked into the Allure lifecycle and produces no results at all. |
| `[AllureParentSuite("...")]` + `[AllureSuite("...")]` + `[AllureSubSuite("...")]` | On the `[TestFixture]` class | Contract tests only — see the table above. `SubSuite` is the class/file name itself. |
| `[AllureEpic("...")]` + `[AllureFeature("...")]` | On the `[TestFixture]` class | Acceptance tests only — see the table above. |
| `[AllureName("...")]` | On each `[Test]` method | A readable display name, analogous to Java's `@DisplayName`/TS's `displayName()`. Use a concrete sentence describing the scenario, not the bare method name. |
| `[AllureStory("...")]` | On a `[Test]` method (optional, acceptance tests only) | The file/class level for acceptance tests, mirroring `SubSuite` above. Add it the moment a feature folder gets a second file — not currently used, since none have yet. |

See `Tests/AcceptanceTests/Contact/ContactCustomerSupportTests.cs` and `Tests/ContractTests/Brand/BrandSpecTests.cs` for the real pattern:

```csharp
// Tests/AcceptanceTests/Contact/ContactCustomerSupportTests.cs — Behaviors hierarchy
[AllureNUnit]
[AllureEpic("AcceptanceTests")]
[AllureFeature("Contact customer support")]
[TestFixture]
public class ContactCustomerSupportTests : ToolshopTest
{
    [Test]
    [AllureName("Guest submits a valid contact form and sees an acknowledgement message")]
    public async Task GuestSubmitsValidContactFormAndSeesAcknowledgement()
    {
        // ...
    }
}
```

```csharp
// Tests/ContractTests/Brand/BrandSpecTests.cs — Suites hierarchy, 3 levels deep
[AllureNUnit]
[AllureParentSuite("ContractTests")]
[AllureSuite("Brand")]
[AllureSubSuite("BrandSpecTests")]
[TestFixture]
public class BrandSpecTests : BrandContractTestBase
{
    // ...
}
```

## Steps

Two separate mechanisms, for two separate jobs — don't conflate them:

- **`[AllureStep]`** (from `Allure.Net.Commons.Attributes`) — decorates an *action method*: a
  page-object action method, an `APIHelper` method, or a common-action method (see
  `common-actions.instructions.md`). Every call becomes a step named from the method, nesting under
  whichever `AllureApi.Step` is currently open. None of the current page objects use this yet. If
  added, it goes on action methods only (never locators), matching the "steps live on actions" rule
  from `page-object.instructions.md`.
- **`AllureApi.Step(name, action)`** (from `Allure.Net.Commons`, the `AllureApi` facade — a
  different namespace than the attributes) — wraps an ad-hoc block *inside a test method* and
  reports it as a named step, with its own pass/fail status in the report. This is how
  `writing-acceptance-tests.instructions.md` structures a test's Given/When/Then: each clause is an
  `await AllureApi.Step("Given ...", async () => { ... })` block instead of a plain comment, so the
  map's own structure is visible in the Allure report's step timeline, not just in the source.
  Overloads: `Step(string)` (an empty marker step), `Step(string, Action)` / `Step(string,
  Func<Task>)` for sync/async bodies, and generic versions that return a value.

```csharp
await AllureApi.Step("Given a registered customer is on the login page",
    () => _loginPage.NavigateToAsync());

await AllureApi.Step("When they submit their correct email and password",
    () => _loginPage.SubmitAsync(Accounts.Customer.Email, Accounts.Customer.Password));

await AllureApi.Step("Then they are logged in and the nav bar shows their account menu",
    () => Assertions.Expect(_navBar.AccountMenuButton).ToHaveTextAsync(Accounts.Customer.Name));
```

## Anti-Patterns ❌

- Importing attributes from `Allure.NUnit.Attributes` instead of `Allure.Net.Commons.Attributes` (deprecated path).
- A `[TestFixture]` without `[AllureNUnit]` — it will run fine under `dotnet test` but silently produce no Allure results.
- Embedding Allure metadata in the test method name itself instead of using the attributes.

## Viewing the report

```bash
dotnet test
allure serve bin/Debug/net10.0/allure-results
```

(Requires the `allure` command-line tool — see the main `README.md`.)
