# Exploration Gate Report - Authentication / Registered customer logs in & logs out

**Grade: 2.1 / 10 (Hard gate)**

Coverage is assessed against the scope found during this exploration pass - a best-effort estimate,
not a proof that nothing was missed.

## Scope inventory

| AC | Weight | Why this weight | Covered by input map? |
| --- | --- | --- | --- |
| AC1 - Valid credentials log the user in | 5 (Critical) | Core happy path | No - mismatch (wrong redirect target) |
| AC2 - Invalid credentials show an error | 5 (Critical) | Security-relevant: must not leak access | Yes |
| AC3 - Required fields are validated | 2 (Medium) | Ordinary validation/UX | No - missing |
| AC4 - Redirect target depends on role | 4 (High) | Gates access to the admin area | No - missing |
| AC5 - TOTP step for 2FA-enabled accounts | 4 (High) | Security-relevant, 2FA bypass risk if wrong | No - missing |
| Logout AC1 - Signing out ends the session | 4 (High) | Security: must actually terminate the session | No - missing (whole Story missing) |

**Score = 10 x (5) / (5+5+2+4+4+4) = 10 x 5/24 = 2.1**

## Concerns
- ⚠ AC1's Then clause claimed "redirected to the home page" - actual behaviour redirects to `/account`. This is a wrong claim, not an absent one - anyone building a test straight from the input map would assert the wrong thing.
- ➕ Missing AC: required-field validation (empty email/password).
- ➕ Missing AC: redirect target depends on role (admin -> `/admin/dashboard`).
- ➕ Missing AC: TOTP/2FA verification step - also carries a ❓ Question: no known seeded account has TOTP enabled, so this AC's Example is provisional until one is found or created.
- ➕ Missing Story: logging out has no representation at all.

## Relationships - needs a human decision
- 🔗 "Forgot your Password?" hands off to the Password Reset feature. Include an Example of arriving here from Login in *that* feature's map, or leave it implicit? Not scored either way - your call.
- 🔗 Successful registration redirects to this Login page rather than auto-authenticating. Worth a cross-feature journey example ("register then log in"), or is that already covered by how Registration's own map handles it? Not scored either way - your call.

## Notes
(none)
