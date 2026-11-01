---
name: writing-acceptance-tests
description: Stage 2 of the existing-feature-acceptance-testing workflow - turning a finalized example map into real test code. Covers file/directory structure, when to parameterize, Given/When/Then structure, assertion style, reuse of existing supporting code, Allure reporting, test-data construction, and tearing down what a test creates. Does not cover implementing page objects, API helpers, fixtures or other utilities - only using what exists, or naming what doesn't yet.
---

# Writing Acceptance Tests (from a finalized example map)

> DRAFT - piloting against a real map before this is treated as settled.

## Scope

Input: a finalized example map (`example-mapping` format, graded by `exploration`). Output: real
test files - directory structure, test methods, Allure annotations, and any test *data* (static
values or `Bogus` factories) they need.

**Out of scope**: implementing page objects, API helpers, fixtures or other utilities. A test
written in this stage may call something that doesn't exist yet - that's expected, not a defect to
work around. See "Coherence over compiling" below.

## 1. Directory / file structure

One Allure Feature = one folder under `Tests/AcceptanceTests/{Area}/`. One Allure Story = one
file/class within it, **only once that Feature has more than one Story** (see `allure.instructions.md`
- this project's existing rule, previously unused because every feature had exactly one file).

- Map's Feature → folder name (e.g. `Authentication`, `Registration`).
- Map's Story → file/class, if there's more than one; otherwise the Feature's one file covers its
  one Story directly (as every existing fixture in this project already does).
- Map's AC → **no dedicated level**. Allure caps at three (Epic → Feature → Story); ACs are just
  grouped as tests within whichever Story file they belong to, in AC order. Use a comment banner per
  AC group if a file covers several, so the map's structure is still visible in the source even
  though Allure's report won't show it as its own node.

New `Data/` files (if any) go under `Data/{Area}/` matching the same Area name as the test folder -
check first whether an existing `Data/` folder for this area already exists under a different name
(this project has at least one such case) and decide per-feature whether to match the new test
folder's name or the existing data folder's name; don't assume they'll already agree.

## 2. Single test or parameterized test

Parameterize (`[TestCase]`) only when **all** of these hold for a group of Examples under one AC:

1. The Given/When/Then steps are the same sequence of calls for every Example - not "similar", the
   same code path.
2. Nothing in the test body branches on which case is running (no `if` on the input/expected value).
3. One consistent assertion call applies to every case, with only the expected *value* varying as a
   parameter - not the kind or shape of the assertion.

If any of these fails, write separate `[Test]` methods even though the Examples share an AC - a
shared AC is not by itself a reason to parameterize. A good test: an AC with many "leave field X
empty, assert message Y" Examples, where only the field name and message change. A bad fit: Examples
that need different setup or prove different things, even if they happen to look similar at a
glance - don't force them together.

NUnit's `[AllureName]` behavior on a parameterized `[TestCase]` hasn't been verified in this project
yet (no existing test is parameterized) - check it empirically the first time, don't assume it
behaves like the non-parameterized case.

## 3. Given/When/Then structure

Direct translation from the AC's own Given/When/Then prose, filled in with the Example's concrete
data - wrap each clause in `AllureApi.Step("<clause text>", ...)` (see `allure.instructions.md`)
rather than a plain comment, so the map's structure shows up as real, individually pass/fail-able
steps in the report, not just as source commentary:

```csharp
await AllureApi.Step("Given a registered customer is on the login page",
    () => _loginPage.NavigateToAsync());

await AllureApi.Step("When they submit their correct email and password",
    () => _loginPage.SubmitAsync(Accounts.Customer.Email, Accounts.Customer.Password));

await AllureApi.Step("Then they are logged in and the nav bar shows their account menu",
    () => Assertions.Expect(_navBar.AccountMenuButton).ToHaveTextAsync(Accounts.Customer.Name));
```

A clause with more than one statement takes a block body (`async () => { await ...; await ...; }`).
Little judgement needed here beyond picking natural variable names for the Example's data - the
clause text itself should read as close to the map's own prose as the concrete Example allows.

## 4. Assertions

Assert **tangible, user-visible/usable state or output data** - something the user could see or use,
not an implementation detail. For a "redirected to X" claim, that means asserting content specific to
the destination (e.g. the nav bar showing the account name, an admin-only nav item being visible) -
**never** `ToHaveURLAsync` as the confirming assertion. A URL check is acceptable only as an
incidental, secondary sanity check that navigation happened at all, never as the thing that proves
the page is correct.

Prefer Playwright's auto-retrying `Assertions.Expect(locator)...Async()` for anything UI-state
related; plain `Assert.That(...)` only for ordinary C# values (see `test.instructions.md`).

## 5. Reuse before writing anything new

Check what already exists before writing a test line: shared reference datasets (`Accounts.cs`),
existing page objects (`NavBar`, etc.), existing fixtures. Use them directly. Where something's
needed but doesn't exist - a new page object, a new page-object member, an API helper - **name it as
if it already existed**, in the shape this project's `page-object`/`api-helper` skills describe, and
move on. Don't stub it, don't implement a minimal version "just to compile" - that's Stage 3's job,
and a half-real stub is worse than an honest gap.

## 6. Allure reporting, and making it filterable

Direct application of the existing rule (`allure.instructions.md`): `[AllureNUnit]` +
`[AllureEpic("AcceptanceTests")]` + `[AllureFeature("<Area>")]` on the class, `[AllureStory("<file
name>")]` on each test method once a Feature folder has more than one file, `[AllureName("<readable
sentence>")]` per test drawn from the AC/Example language.

Also add NUnit's own `[Category("<Area>")]` on the class alongside the Allure attributes - this is
what makes the Feature runnable via `dotnet test --filter "TestCategory=<Area>"`. The two are
separate systems serving separate consumers (Allure's attributes never reach the test runner's
filter - see `allure.instructions.md`), so both go on, together, every time.

## 7. Test data construction

This is the one part of this stage whose output is real, compiling code, not aspirational calls -
see `test-data.instructions.md` for the full rule. Check what already exists, in order, before
writing anything new:

1. **A shared reference dataset already fits** (e.g. an account from `Accounts.cs`) → use it
   directly.
2. **A dynamic factory already fits** (e.g. `ContactMessageFactory`) → use it directly.
3. **Nothing existing fits** → construct the data inline in the test file (a private helper method,
   or inline `Bogus` calls), rather than naming a new factory ahead of time. Unlike page
   objects/API helpers (Section 5), test-data *shape* is not something to presuppose aspirationally -
   build it for real, here, now. Extracting it into a proper `Data/{Area}/{Name}Factory.cs` is a
   Refactor-stage call, made once a second test file actually needs the same shape.
4. **A narrow expected value** (one error string, asserted by exactly one test file) → inline in
   that test file, not centralised.

User accounts specifically: default to a shared seeded account unless the test's own action mutates
it (lockout, disabling, enabling TOTP, a password change) - see "User Accounts" in
`test-data.instructions.md`. A mutating test builds and tears down a throwaway account instead (see
below).

## 8. Tearing down what a test creates

A test that builds a throwaway account (or any other stateful data) for itself must also remove it -
don't leave it behind for the suite to accumulate. Write the cleanup as an explicit, visible step,
not an implicit assumption:

- Use NUnit's `[TearDown]`, wrapped in its own `AllureApi.Step("Cleanup: ...", ...)` the same as any
  other step, so it shows up in the report and still runs whether the test passed or failed.
- Call an existing deletion/cleanup method on whatever owns the data (e.g. the API helper) if one
  exists. If it doesn't, name the prospective method the same way Section 5 names any other
  not-yet-built supporting code - aspirational, not stubbed.
- A test that only reads through a shared seeded account (Section 7) has nothing of its own to tear
  down - `[TearDown]` should no-op for it, not call cleanup unconditionally.

```csharp
private string? _createdAccountEmail;

[TearDown]
public async Task TearDown()
{
    if (_createdAccountEmail is null) return;

    await AllureApi.Step("Cleanup: delete the throwaway account created for this test",
        () => _apiHelper.DeleteAccountAsync(_createdAccountEmail));
}
```

## Coherence over compiling

Stage 2's tests are judged on whether they're coherent, readable, and consistent with the project's
existing tests - not on whether the project currently compiles. A test method calling a page-object
member that doesn't exist yet is expected, not a mistake: write the call exactly as it should read
once Stage 3 adds the member, in the shape `page-object.instructions.md` describes. The project is
expected to be red between Stage 2 and Stage 3 within the same pass - this is not a shared branch
other work depends on staying green in the meantime.
