---
name: test-data
description: Conventions for test data in this project: when to use a dynamic factory vs a static constant, and where each lives. Use before adding new test input data.
---

# Test Data Instructions

## Strategy

| Tier | What | Where | Real example |
| --- | --- | --- | --- |
| **Shared reference dataset** | A pool of known, structured data that more than one test file selects specific items from | `Data/{Area}/{Name}.cs` — `static class` with `const`/`static readonly` members | `Data/Auth/Accounts.cs` (seeded accounts several contract test files pick from by name) |
| **Narrow expected value** | A single fixed string asserted by exactly one test file (an error message, a success message) | Inline in the test file itself, where it's asserted | — |
| **Dynamic** | Names, emails, messages that must be unique/realistic | `Data/{Area}/{Name}Factory.cs` — `Bogus`-backed factory method returning a `record` | `Data/Contact/ContactMessageFactory.cs` |
| **Binary fixtures** | File upload inputs (txt, pdf, etc.) | `Data/Fixtures/{file}`, marked `CopyToOutputDirectory` in the `.csproj` | `Data/Fixtures/valid-example-file.txt` |

**Centralise a static value in `Data/` only when it's a genuine shared reference dataset** — a pool of known, structured data (seeded accounts, a seeded product catalog) that more than one test file draws specific items from. A one-off expected value tied to a single test's assertion (one error string, asserted in exactly one file) stays inline in that test file, even if nothing in principle stops another test from wanting the same string later — pulling it into `Data/` for that reason alone adds a layer of indirection without removing any real duplication. If a second file later needs the same value, that's the signal to promote it into `Data/`, not before (mirrors the "promote once a second consumer appears" rule contract tests already follow for shared setup — see `contract-testing.instructions.md`).

Dynamic data is unaffected by this distinction — always generate with `Bogus`, never hardcode a name/email that needs to be unique or realistic. But a dynamic factory *file* follows the same promote-on-second-consumer rule as a static dataset: when a test needs throwaway dynamic data and no `Data/{Area}/{Name}Factory.cs` already fits, build it inline in the test file (a private helper method, or inline `Bogus` calls) rather than naming a new factory ahead of time. Extracting it into `Data/` is a Refactor-stage call, made once a second test file actually needs the same shape — not before.

---

## Dynamic Test Data with Bogus

`Bogus` is the .NET equivalent of Java's `DataFaker` / TypeScript's `@faker-js/faker`. Return a `record`, not a loose tuple, so call sites are self-documenting:

```csharp
// Data/Contact/ContactMessageFactory.cs
public record ContactMessage(string FirstName, string LastName, string Email, string Subject, string Message);

public static class ContactMessageFactory
{
    private static readonly Faker Faker = new();

    public static ContactMessage BuildGuestContactMessage()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        return new ContactMessage(
            FirstName: Faker.Name.FirstName(),
            LastName: Faker.Name.LastName(),
            Email: $"test+{uniqueId}@example.com",
            Subject: "Customer service",
            Message: Faker.Lorem.Sentences(2).PadRight(55, '.'));
    }
}
```

```csharp
// In test
var data = ContactMessageFactory.BuildGuestContactMessage();
await _contactForm.SubmitAsGuestAsync(data.FirstName, data.LastName, data.Email, data.Subject, data.Message);
```

## Shared Reference Datasets

Use a `static class` for a pool of known, structured data that more than one test *file* draws
specific items from — see `Data/Auth/Accounts.cs`, where several contract test files each pick a
named account (`Accounts.Customer`, `Accounts.Admin`, ...) rather than inventing their own.

```csharp
// In test
var account = Accounts.Customer;
```

`Data/Contact/ContactMessages.cs` predates this distinction and is a `const string` class asserted
by exactly one file (`ContactCustomerSupportTests.cs`) — under the rule above it wouldn't be
centralised today. It's left as-is rather than refactored for its own sake, but don't copy its shape
for a new narrow expected value; inline that instead (see below).

## User Accounts

Accounts are a different kind of test data than product/content or message data — identity, not
inventory — so the default is reuse, not generation:

- **Default to a shared seeded account** (`Accounts.Customer`, `Accounts.Admin`, ...) whenever a
  test only *reads* through an account (logs in, checks something) without leaving it in a different
  state than before.
- **Build a throwaway account instead** only when the test's own action would mutate that account in
  a way other tests can't share — locking it out, disabling it, enabling TOTP, changing its
  password. Construct it inline in the test file (per the promotion rule above — there's no existing
  registration factory yet) and tear it down afterward; see "Tearing down what a test creates" in
  `writing-acceptance-tests.instructions.md` for how the cleanup itself is written.
- Never mutate a shared seeded account's state as a side effect of an otherwise-read-only test — that
  silently breaks every other test that assumed it was still in its original state.

## Narrow Expected Values

A fixed string asserted by exactly one test file (an error message, a success message) stays
**inline in that test file**, right where it's asserted - not in `Data/`:

```csharp
// In test, e.g. LoginTests.cs
await Assertions.Expect(_loginPage.ErrorAlert).ToHaveTextAsync("Invalid email or password");
```

If a second file later needs the same string, that's the signal to promote it into a `Data/{Area}/`
static class - not before.

## Binary Fixtures

Store file-upload inputs under `Data/Fixtures/`. They must be listed in `Toolshop.Playwright.Tests.csproj` with `CopyToOutputDirectory` so they exist next to the test binaries at run time, and referenced via `AppContext.BaseDirectory` (not a path relative to the source tree, which won't resolve at test-run time):

```csharp
var attachment = Path.Combine(AppContext.BaseDirectory, "Data", "Fixtures", "valid-example-file.txt");
await _contactForm.AttachFileAsync(attachment);
```

Current fixtures: `valid-example-file.txt` (empty — the app only accepts empty `.txt` uploads), `invalid-example-file-size.txt` (non-empty, triggers the size-validation error), `invalid-example-file-format.pdf` (wrong extension, triggers the format-validation error).

## Anti-Patterns ❌

- Hardcoding emails, passwords, or names directly in test files.
- Using `Random`/`Guid.NewGuid()` alone for "realistic" data instead of `Bogus` (a short `Guid` suffix for uniqueness, as in `ContactMessageFactory`, is fine — generating the whole value that way is not).
- Referencing a `Data/Fixtures/*` file by a path relative to the test source file instead of `AppContext.BaseDirectory`.
