---
name: utilities
description: Where pure, stateless helper logic with no Playwright/API dependency belongs (e.g. computing a TOTP code from a secret), as part of Stage 3 (Supporting code) of the existing-feature-acceptance-testing workflow. Use before adding a static helper that isn't a page object, API helper, common action, or test-data factory.
---

# Utilities

> DRAFT - piloting against a real feature before this is treated as settled.

## What this is

A utility is a pure function of its inputs: no `IPage`, no `IAPIRequestContext`, no side effect on
the running app at all. Given the same input it always returns the same output. If it needs either
of those two things, it isn't a utility - see the table below for where it actually belongs.

Concrete example: computing the current TOTP code from a secret (`TotpCodeGenerator.ComputeCurrentCode(secret)`,
referenced aspirationally from `LoginTests.cs`). The secret came from the app (via a common action
calling the API), but *computing the code from it* is pure math - no further call to the app at all.

## Which category is this, really?

| Needs `IPage`? | Needs `IAPIRequestContext`? | Calls more than one page object/API helper? | → |
| --- | --- | --- | --- |
| Yes | - | - | Page object (`page-object.instructions.md`) |
| - | Yes | - | API helper (`api-helper.instructions.md`) |
| Yes and/or Yes | - | Yes, coordinated together | Common action (`common-actions.instructions.md`) |
| No | No | - | **Utility** |

Building a `record`/data shape is a different concern again (test-data, not a utility) - a utility
*computes* something from data it's given, it doesn't construct the data itself.

## File Location

```
Utilities/{Name}.cs
```

A `static class` with `static` methods - no instance state, nothing to construct in `[SetUp]`,
called directly: `TotpCodeGenerator.ComputeCurrentCode(secret)`.

## Pattern

```csharp
namespace Toolshop.Playwright.Tests.Utilities;

public static class TotpCodeGenerator
{
    public static string ComputeCurrentCode(string secret)
    {
        // real TOTP (RFC 6238) computation - reach for a well-vetted library rather than
        // hand-rolling the HMAC/base32 logic; see the note on this when it's actually implemented.
    }
}
```

## Anti-Patterns ❌

- A "utility" that calls `Page` or `IAPIRequestContext` - it's a page object, API helper, or common
  action instead, not this.
- Hand-rolling cryptographic or well-specified-algorithm logic (TOTP, hashing, signing) instead of
  using a vetted library - a utility wrapping a library call is still a utility; reimplementing the
  algorithm by hand is a correctness and security risk for no benefit.
- An instance class with constructor dependencies - if it needs something injected, it isn't
  stateless, and it isn't a utility.
