# toolshop-playwright-csharp

Playwright + NUnit end-to-end tests for the [Practice Software Testing](https://practicesoftwaretesting.com)
toolshop app, written in C#/.NET. Sibling of `toolshop-playwright-java` and
`toolshop-playwright-typescript` — same app under test, different stack.

## Requirements

- .NET SDK 10.0+

## Setup

```bash
dotnet restore
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install chromium   # one-time browser download
```

## Running tests

```bash
dotnet test
```

By default, UI tests run headless against `https://practicesoftwaretesting.com` (override with
`BASE_URL`), while API/contract tests default to the local dev stack at
`http://localhost:8091` (override with `API_URL`) so they exercise and mutate a disposable
local database rather than the public live demo:

```bash
BASE_URL=http://localhost:4200 API_URL=http://localhost:8091 dotnet test   # explicit, same as the API_URL default
BASE_URL=https://practicesoftwaretesting.com API_URL=https://api.practicesoftwaretesting.com dotnet test   # point everything at the live site instead
```

Run headed (visible browser) or in a different browser via Playwright's own env vars:

```bash
HEADED=1 dotnet test
BROWSER=firefox dotnet test
```

Run a single fixture or test:

```bash
dotnet test --filter "FullyQualifiedName~ContactCustomerSupportTests"
dotnet test --filter "Name=GuestSubmitsValidContactFormAndSeesAcknowledgement"
```

## Viewing the Allure report

Tests write Allure results to `bin/Debug/net10.0/allure-results/`. With the
[Allure commandline](https://allurereport.org/docs/install/) installed:

```bash
allure serve bin/Debug/net10.0/allure-results
```

## Project layout

```
Fixtures/   ToolshopTest.cs       - shared PageTest base: base URL + data-test attribute wiring
Pages/      NavBar.cs, ContactPage.cs, ProductListPage.cs
Tests/      AcceptanceTests/{Contact,Catalog}/  - UI tests (NUnit [TestFixture], derive from ToolshopTest)
            ContractTests/Brand/               - API contract tests (derive from ApiTest)
Data/       Contact/              - Bogus-based factories and static constants
            Fixtures/             - binary file-upload fixtures
```

See `AGENTS.md` for the full conventions, and `.claude/skills/*/SKILL.md` for detail per
concern (page objects, tests, test data, Allure, API helpers, the `playwright-cli` tool).
