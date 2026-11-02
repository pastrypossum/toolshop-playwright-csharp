# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

An **external repo** (deliberately separate from the application's own source) for **E2E acceptance testing and contract testing** of the "toolshop" app — verifying it behaves correctly from the outside, against its real UI and API, rather than testing its internals. Concretely, it's a Playwright + NUnit E2E test suite (C#/.NET): UI tests run against `https://practicesoftwaretesting.com` by default, while API/contract tests default to the local dev stack (`http://localhost:8091`, i.e. `docker-compose up` in `practice-software-testing`) so they exercise and mutate a disposable local database rather than the public live demo - see `Fixtures/ToolshopTest.cs`. One of three sibling projects testing the same app in different languages — see `toolshop-playwright-java` (Maven/JUnit5) and `toolshop-playwright-typescript` (npm/@playwright/test) for cross-language reference implementations. This project intentionally covers less ground than those two (contact-support flow + basic catalog search) and is meant to be extended following the conventions below, not reinvented.

## Commands

```bash
# First-time setup
dotnet restore
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium   # one-time browser download, not covered by restore/build

dotnet test                                               # run everything
dotnet test --filter "FullyQualifiedName~ContactCustomerSupportTests"   # one fixture
dotnet test --filter "Name=GuestSubmitsValidContactFormAndSeesAcknowledgement"  # one test

BASE_URL=http://localhost:4200 dotnet test                 # also point UI tests at the local dev server (API_URL already defaults there)
BASE_URL=https://practicesoftwaretesting.com API_URL=https://api.practicesoftwaretesting.com dotnet test   # point everything at the live site instead
HEADED=1 dotnet test                                       # visible browser
BROWSER=firefox dotnet test                                 # different browser

allure serve bin/Debug/net10.0/allure-results               # view the Allure report
```

Requires .NET SDK 10.0+.

## Architecture

```
Fixtures/   ToolshopTest.cs     - shared PageTest base: BaseURL (BASE_URL env var, defaults to the live site) + data-test attribute wiring; also defines ApiUrl (API_URL env var, defaults to the local dev stack) for Fixtures/ApiTest.cs
Pages/      NavBar.cs, ContactPage.cs, ProductListPage.cs
Tests/      AcceptanceTests/{Contact,Catalog}/  - UI tests (derive from ToolshopTest)
            ContractTests/Brand/               - API contract tests (derive from ApiTest)
Data/       Contact/            - Bogus-based factories + static constants
            Fixtures/           - binary file-upload fixtures (copied to output dir via .csproj)
```

- **`Fixtures/ToolshopTest.cs`** — every test fixture derives from this, never from `Microsoft.Playwright.NUnit.PageTest` directly. It overrides `ContextOptions()` for `BaseURL` and calls `Playwright.Selectors.SetTestIdAttribute("data-test")` in `[SetUp]` — Playwright's .NET default test-id attribute is `data-testid`, which this app does not use, so this wiring is load-bearing for every `GetByTestId()` call.
- **Test classes** (`Tests/{Area}/`): end in `Tests`, decorated `[AllureNUnit]` + `[AllureSuite("...")]` on the class, `[AllureName("...")]` on each `[Test]` method — a fixture missing `[AllureNUnit]` silently produces no Allure results. Page objects are constructed in `[SetUp]`.
- **Page objects** (`Pages/`): constructor takes `IPage` via a primary constructor (e.g. `public class ContactPage(IPage page)`); locators exposed as `ILocator` get-only properties; action methods are `async Task`, suffixed `Async`; no assertions inside page objects.
- **Assertions**: `Microsoft.Playwright.Assertions.Expect(locator)` for auto-retrying UI state checks; plain NUnit `Assert.That(...)` for ordinary value assertions.
- **Test data**: dynamic via `Bogus` factories in `Data/{Area}/{Name}Factory.cs` returning a `record`; static via `const string` fields on a `static class`.
- **Allure reporting**: `Allure.NUnit` + `Allure.Net.Commons` (attributes from `Allure.Net.Commons.Attributes`, the current non-deprecated namespace). Results land in `allure-results/` next to the test binaries, configured via `allureConfig.json`.
- **Known gaps** (intentional, not oversights): no API helper layer yet (see the `api-helper` skill for the pattern to follow when needed — it belongs at `Fixtures/APIHelper.cs`, not a separate folder); `Deque.AxeCore.Playwright` is installed but no test runs an accessibility scan with it yet.

## Parallelism rules

Tests run with `[assembly: Parallelizable(ParallelScope.All)]` + `[assembly: LevelOfParallelism(8)]` (`AssemblyInfo.cs`) - fixtures run concurrently with each other, and a fixture's own tests run concurrently with each other too, up to 8 workers. Contract tests run under this default as-is. UI tests override back to fully sequential, including with each other (`[Parallelizable(ParallelScope.None)]` on `Fixtures/ToolshopTest.cs`, which NUnit honors over the assembly default - confirmed empirically, not just assumed, by comparing summed per-test durations against total wall time across several runs). Found the hard way while making this work - all of these still apply to any new test:

- **Never use a `static` field for test-scoped state.** `[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` gives every test its own fixture instance, but a `static` field is shared across all of them regardless - it silently reintroduces the exact cross-test interference that setting exists to prevent. Instance fields set in `[SetUp]`/the test itself only.
- **Don't assert exact state on a shared live collection** (`GetArrayLength() == N`, an exact count) if other tests may be concurrently creating/deleting into it. Assert the presence of specific, permanent, known data instead - only safe to assert collection-wide state if nothing in the suite ever changes what's in it.
- **Know which `ParallelScope` you're choosing, not just that you chose one.** `All` parallelizes both fixtures *and* the test methods within each one; `Fixtures` only parallelizes across files, keeping a file's own tests sequential. The two look like a one-word difference in `AssemblyInfo.cs` but produce very different failure modes - `All` caused a hard crash here (`TargetClosedException`, concurrent methods on one shared-ish fixture overwriting each other's `Page`) before `[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` existed to give each concurrently-running test its own fixture instance. The assembly now runs under `All` again, deliberately, for exactly that reason - it's safe for Contract tests (no shared browser `Page`) with that lifecycle setting in place. It is *not* safe for browser-backed tests regardless of `FixtureLifeCycle` (see the next point) - that's what `ToolshopTest`'s own `None` override exists to keep true.
- **A new browser-backed test base class defaults to non-parallel** (`[Parallelizable(ParallelScope.None)]`), like `ToolshopTest`. Real browser launches contend for the shared local dev server in a way API calls don't, and that failure mode (`TimeoutException`, not a logic bug) only shows up under genuine concurrent load. The threshold is lower than instinct suggests: even just 2 concurrent browsers (one per UI fixture, the gentlest possible step up from fully sequential) was unreliable here - don't assume a small amount of UI concurrency is obviously fine just because the aggressive version wasn't. Widen only with evidence, starting from the lowest level, not by assumption.
- **A parallel-safety claim needs several consecutive clean runs as evidence, not one.** The whole bug class is nondeterministic - a single run can look clean by chance (or fail by chance) in either direction. This project settled on 3 consecutive clean runs as the bar before trusting a configuration.

### Further conventions

- `AGENTS.md` — full conventions overview (same spirit as this file, slightly more detail).
- `.claude/skills/{page-object,test,test-data,allure,api-helper}/SKILL.md` — focused rules per authoring topic, each grounded in this project's actual code.
- `.claude/skills/playwright-cli/` — skill for driving a real browser interactively via the `playwright-cli` tool during development (distinct from the automated test suite).
- `.claude/copilot-instructions.md` — a legacy GitHub Copilot doc, parked here (not deleted or merged) for manual review; not kept in sync with the skills above.
- `.github/workflows/playwright.yml` — CI: `dotnet restore`/`build`/`test` + Playwright browser install on push/PR.
