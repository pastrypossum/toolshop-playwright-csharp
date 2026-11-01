# Exploration Gate Report (Iteration 4) - Authentication / Registered customer logs in & logs out

**Grade: 10 / 10 (Informational - out of the warning zone)**

Coverage is assessed against the scope found during this exploration pass - a best-effort estimate,
not a proof that nothing was missed.

AC5 now counts as covered: not because a real TOTP code was captured (that's inherently impossible
to write down - codes rotate every 30 seconds), but because the Example's shape is now fully and
correctly specified, including *why* its one dynamic field can never be a fixed value and where the
account that supplies it comes from (created and torn down per test, not a shared fixture - see the
map's note on AC5).

## Scope inventory

| AC | Weight | Covered? |
| --- | --- | --- |
| AC1 - Valid credentials log the user in | 5 | Yes |
| AC2 - Invalid credentials show an error | 5 | Yes |
| AC3 - Required fields are validated | 2 | Yes |
| AC4 - Redirect target depends on role | 4 | Yes |
| AC5 - TOTP step for 2FA-enabled accounts | 4 | Yes - account create/use/discard pattern and runtime-computed code documented |
| AC6 - Account locked after 3 failed attempts | 5 | Yes |
| AC7 - Disabled account rejected | 4 | Yes |
| Logout AC1 | 4 | Yes |

**Score = 10 x 33 / 33 = 10**

## Concerns
(none remaining)

## Questions - final status
- ✅ Answered: TOTP account/code source (see map).
- ⏸ Postponed to Stage 3: whether a successful TOTP login resets `failed_login_attempts` the same way a password-only login does - needs closer reading of `UserService`'s attempt-counter logic than this exploration pass covered.

## Relationships - needs a human decision
(unchanged from iteration 1 - see gate-report-1 - still open, not scored)

## Side effect of this exploration pass
A throwaway account (`totp-pilot-20261101@example.test`) was registered on the local instance while
investigating AC5, then abandoned mid-flow once the "create it as part of the test itself" direction
came in. It was never TOTP-enabled. Harmless on a disposable local dev DB, but flagging it rather
than leaving it a silent side effect.
