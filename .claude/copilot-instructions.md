# Copilot Instructions for toolshop-playwright-csharp

## Project Overview

This is a Playwright-based end-to-end test automation framework written in C#/.NET (NUnit). It
tests the Practice Software Testing toolshop application and generates Allure reports for test
execution results. It is the C# sibling of `toolshop-playwright-java` (Maven/JUnit5) and
`toolshop-playwright-typescript` (npm/@playwright/test) — same app under test, different stack,
smaller current scope (contact-support flow + basic catalog search).

## Build & Test Commands

**.NET Setup:**
- .NET SDK 10.0+ required (`dotnet --version`)
- Playwright browser binaries are a separate one-time step — see below

**First-time setup:**
```bash
dotnet restore
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

**Running Tests:**
```bash
# Run all tests
dotnet test

# Run a single fixture
dotnet test --filter "FullyQualifiedName~ContactCustomerSupportTests"

# Run a single test method
dotnet test --filter "Name=GuestSubmitsValidContactFormAndSeesAcknowledgement"

# Point at a local dev server instead of the live site
BASE_URL=http://localhost:4200 API_URL=http://localhost:8091 dotnet test

# Run headed / in a different browser
HEADED=1 dotnet test
BROWSER=firefox dotnet test
```

**Build Only (without tests):**
```bash
dotnet build
```

**Viewing the Allure report:**
```bash
allure serve bin/Debug/net10.0/allure-results
```

## Architecture

### Test Structure

- **Page Object Model (POM)**: Tests use page objects to encapsulate UI interactions.
  - Page objects live in `Pages/`.
  - Each page object represents a UI component or page and exposes locators + action methods.
  - Tests reference page objects rather than calling `Page.Locator(...)` directly.

- **Base Test Fixture**:
  - `Fixtures/ToolshopTest.cs` extends `Microsoft.Playwright.NUnit.PageTest`.
  - Overrides `ContextOptions()` to set `BaseURL` from the `BASE_URL` env var (default: the live
    UAT site).
  - Configures the app's `data-test` attribute as the test-id attribute in a `[SetUp]` method
    (`Playwright.Selectors.SetTestIdAttribute("data-test")`) — Playwright's .NET default is
    `data-testid`, which this app does not use.
  - Every test fixture derives from `ToolshopTest`, not `PageTest` directly.

- **Test Classes**: Located in `Tests/{Area}/`.
  - `[TestFixture]` classes decorated with `[AllureNUnit]` + `[AllureSuite("...")]`.
  - `[Test]` methods decorated with `[AllureName("...")]` for Allure report readability.
  - Page objects constructed in `[SetUp]`.

- **Reporting**: Allure via the `Allure.NUnit` + `Allure.Net.Commons` packages.
  - Results written to `allure-results/` next to the test binaries (configured in
    `allureConfig.json`, copied to output via the `.csproj`).
  - Attributes come from `Allure.Net.Commons.Attributes` (the current, non-deprecated
    namespace as of `Allure.NUnit` 2.15.0) plus `[AllureNUnit]` from `Allure.NUnit` itself.

### Key Dependencies

- **Microsoft.Playwright 1.63.0**: Core UI automation framework.
- **Microsoft.Playwright.NUnit 1.63.0**: `PageTest`/`ContextTest`/`BrowserTest` base classes and the NUnit test adapter integration.
- **NUnit 4.3.2** + **NUnit3TestAdapter 5.0.0**: Test runner.
- **Allure.NUnit 2.15.0** / **Allure.Net.Commons 2.15.0**: Test reporting framework.
- **Bogus 35.6.5**: Generate realistic test data (.NET equivalent of Java's DataFaker / TS's `@faker-js/faker`).
- **Deque.AxeCore.Playwright 4.14.0**: Accessibility testing (added as a dependency; no test in this project exercises it yet — see "Known gaps" below).

## Key Conventions

### Test Class Naming & Structure
- Test classes live in `Tests/{Area}/` and end with `Tests` (e.g. `ContactCustomerSupportTests`, `ProductSearchTests`).
- Derive from `ToolshopTest` (never `PageTest` directly).
- Use `[AllureNUnit]` + `[AllureSuite("...")]` on the class, `[AllureName("...")]` on every `[Test]` method.
- Construct page objects in a `[SetUp]` method.

### Page Objects
- One page object class per logical UI component, in `Pages/`.
- Constructor takes `IPage` via a primary constructor: `public class ContactPage(IPage page)`.
- Methods describe user actions (e.g. `SubmitAsGuestAsync(...)`, `SearchForAsync(string query)`) and are `async Task`, suffixed `Async`.
- Use `Page.GetByTestId()`/`GetByRole()`/`GetByLabel()` over raw CSS selectors wherever the app exposes them.
- Expose feedback/validation locators (success alerts, field errors) as `ILocator` properties so tests assert against them directly — never assert inside the page object.

### Selectors & Locators
- Use the `data-test` attribute for test ids (wired up in `Fixtures/ToolshopTest.cs`).
- `Page.GetByRole()`, `Page.GetByLabel()`, `Page.GetByTestId()` are preferred over CSS selectors.
- Store commonly-used locators as `ILocator` properties for reuse within a page object.

### Assertions
- Use Playwright's own auto-retrying assertions, `Microsoft.Playwright.Assertions.Expect(locator)`, for UI state (visibility, text content).
- Use plain NUnit `Assert.That(...)` for ordinary C# value assertions (e.g. a `List<string>` returned from a page object).

### Test Data
- Dynamic data: `Bogus`-based factory methods in `Data/{Area}/{Name}Factory.cs`, returning a `record`.
- Static data: `const string` fields on a `static class` in `Data/{Area}/{Name}.cs`.
- Binary upload fixtures: `Data/Fixtures/`, copied to the output directory via the `.csproj`, referenced via `AppContext.BaseDirectory`.

## Common Development Tasks

### Adding a New Test
1. Create (or reuse) a test class in `Tests/{Area}/` ending in `Tests`, deriving from `ToolshopTest`.
2. Add `[AllureNUnit]` + `[AllureSuite("...")]` on the class.
3. Construct needed page objects in `[SetUp]`.
4. Write `[Test]` methods with `[AllureName("...")]`, using page object methods and `Assertions.Expect`/`Assert.That`.

### Adding a New Page Object
1. Create a class in `Pages/` taking `IPage` via a primary constructor.
2. Expose locators as `ILocator` get-only properties.
3. Implement `async Task` action methods for user interactions; add a `NavigateToAsync()` only if the page has its own route.

### Viewing Test Results
```bash
dotnet test
allure serve bin/Debug/net10.0/allure-results
```

## Known gaps (intentionally out of scope for now)
- No API helper layer yet (see `.github/instructions/api-helper.instructions.md` for the pattern to follow when one is needed).
- `Deque.AxeCore.Playwright` is installed but no test currently runs an accessibility scan with it.
- `.github/workflows/playwright.yml` runs `dotnet restore`/`build`/`test` plus the Playwright browser-install step on push/PR to `main`/`master`, mirroring the TypeScript project's workflow (the Java project currently has none).

---

**Note**: This project intentionally covers less ground than its Java and TypeScript siblings — the contact-support flow and basic catalog search — and is meant to be extended following the conventions above, not reinvented per change.
