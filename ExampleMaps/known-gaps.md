# Known Gaps

Real, confirmed discrepancies between a feature's intended/documented behaviour and its actual live
behaviour - found during exploration, or during Stage 2-4 of the `existing-feature-acceptance-testing`
workflow. Each entry is something that can be cross-checked against the running application and the
test suite at any time, not just recorded once and forgotten - re-verify an entry (and update its
Status) whenever the covering test's result changes, or when deliberately re-checking this file.

A gap only belongs here once it's **confirmed**, not just suspected - against the live app, a direct
API call, or (if neither is possible yet) a clear reading of the source that a later pass should
still live-verify. "The map says X but I haven't checked" is a ❓ Question in the map itself, not an
entry here.

## Format

```
### <Feature> - <short title>
- **Found:** <date>, during <stage/activity>
- **Confirmed:** <how, and what makes it a real gap, not just unverified>
- **Covered by:** <test file + method - the deliberately-red test documenting this, per
  `writing-acceptance-tests.instructions.md`'s known-gap convention>
- **Status:** Open / Fixed (<date>, <how re-confirmed>) / Accepted (won't fix - <why>)
```

---

### Registration - the 75-year age ceiling is not actually enforced

- **Found:** 2026-11-02, during Registration Stage 3.
- **Confirmed:** `API/app/Http/Requests/Customer/StoreCustomer.php`'s `rules()` declares `dob` must
  be `after:` 75 years ago - but direct `POST /users/register` calls against the local dev stack
  with dob values for age 75, 76 and 80 all returned `201` (success). The rule exists in source but
  doesn't actually reject anyone at the stated boundary.
- **Covered by:** `Tests/AcceptanceTests/Registration/RegistrationTests.cs` -
  `TooOldIsNotActuallyRejected` (asserts the intended/documented behaviour; deliberately red until
  the real bug is fixed).
- **Status:** Open.
