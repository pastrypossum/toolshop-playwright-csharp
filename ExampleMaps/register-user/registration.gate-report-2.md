# Exploration Gate Report (Iteration 2) - Registration / Guest creates an account

**Grade: 10 / 10 (Informational - out of the warning zone)**

Coverage is assessed against the scope found during this exploration pass - a best-effort estimate,
not a proof that nothing was missed. Grading the updated `registration.map.reviewed.md` (AC6-AC9
added, AC2 expanded to all 12 required-field examples) against the full inventory below.

## Scope inventory

| AC | Weight | Covered? |
| --- | --- | --- |
| AC1 - Valid details create an account | 5 | Yes |
| AC2 - Required fields are validated | 2 | Yes (all 12 fields exampled) |
| AC3 - Password complexity | 4 | Yes (real 8-char threshold correctly asserted; copy-mismatch tracked as an open Question, not a content gap) |
| AC4 - Postal code lookup | 2 | Yes |
| AC5 - Duplicate email rejected | 4 | Yes |
| AC6 - Age restriction (18-75) | 2 | Yes |
| AC7 - Breached-password rejection | 4 | Yes |
| AC8 - Field length limits | 1 | Yes (source-confirmed, not yet live-exercised - same standard as Login's AC6/AC7) |
| AC9 - Subscript/superscript rejection | 1 | Yes (source-confirmed, not yet live-exercised) |

**Score = 10 x 25 / 25 = 10**

## Concerns
(none remaining that affect content coverage)

## Questions - final status
- 🚫 **Disregarded** (by the map's owner): AC3's displayed error says "minimal 6 characters" while
  the real enforced minimum is 8. Not being tracked as a bug - the map still asserts the real
  behaviour (8) since that's what a test must assert regardless of the disregarded copy question.

## Relationships - needs a human decision
- 🔗 Registration hands off to Login (no auto-authentication). Mirrors the relationship already
  flagged from the Login side. Not scored either way.
