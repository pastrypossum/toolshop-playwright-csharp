# Exploration Gate Report (Iteration 3) - Authentication / Registered customer logs in & logs out

**Grade: 8.8 / 10 (Advisory - not yet out of the warning zone)**

Grading the updated `authentication-login.map.reviewed.md` (now including AC6 lockout and AC7
disabled-account) against the same 8-AC inventory as iteration 2.

## Scope inventory

| AC | Weight | Covered? |
| --- | --- | --- |
| AC1 - Valid credentials log the user in | 5 | Yes |
| AC2 - Invalid credentials show an error | 5 | Yes |
| AC3 - Required fields are validated | 2 | Yes |
| AC4 - Redirect target depends on role | 4 | Yes |
| AC5 - TOTP step for 2FA-enabled accounts | 4 | **No** - mechanism and exact copy are now fully documented from source, but the Example still has no concrete data (no account exists to run it against) |
| AC6 - Account locked after 3 failed attempts | 5 | Yes (source-confirmed, not yet live-exercised - see verification note in the map) |
| AC7 - Disabled account rejected | 4 | Yes (source-confirmed, not yet live-exercised) |
| Logout AC1 | 4 | Yes |

**Score = 10 x (5+5+2+4+0+5+4+4) / 33 = 10 x 29/33 = 8.8**

## Concerns
- ➕ Missing Example (AC5): the rule and exact error copy are fully known, but there is no concrete
  account to put in the Given column. This is the only thing between this map and a 9-10.

## Why AC6/AC7 count as covered but AC5 doesn't

AC6 and AC7 have complete concrete data and exact copy straight from source, just not yet clicked
through in the browser - that's a live-verification gap, not a content gap. AC5's Example column is
still a placeholder - nobody could pick up this map and run that Example, because the data it needs
doesn't exist yet. Treating that as "covered" would mean accepting a guess dressed up as an Example,
which the `example-mapping` skill explicitly treats as an anti-pattern.

## Open question for you

Closing this gap means either:
(a) actually creating a TOTP-enabled account (register one, use its own self-service "enable 2FA"
    setting, use the real secret) so AC5 gets real data - which is arguably fixture/setup work,
    not "exploration" in the read-only sense the rest of this process has used so far, or
(b) accepting that a map can sit just below the 9-10 band indefinitely on a legitimately open
    Question, with no path to "final" until someone (a human, or a later stage) resolves it.

Which of these should Stage 1 be allowed to do - is creating a throwaway fixture account in-scope
for exploration, or does that belong in Stage 3 (supporting code) instead?

## Relationships - needs a human decision
(unchanged from iteration 1 - see gate-report-1)
