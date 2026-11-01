# Exploration Gate Report (Iteration 2) - Authentication / Registered customer logs in & logs out

**Grade: 6.1 / 10 (Advisory)**

Coverage is assessed against the scope found during this exploration pass - a best-effort estimate,
not a proof that nothing was missed. This pass widened the code search from the Angular login
component (`UI/src/app/auth/login/`) to the backend (`API/app/Services/UserService.php`), which is
why the inventory grew since iteration 1.

Grading `authentication-login.map.reviewed.md` (iteration 1's output - itself unchanged since the
last report) against this wider inventory.

## Scope inventory

| AC | Weight | Why this weight | Covered by map.reviewed.md? |
| --- | --- | --- | --- |
| AC1 - Valid credentials log the user in | 5 (Critical) | Core happy path | Yes |
| AC2 - Invalid credentials show an error | 5 (Critical) | Security-relevant | Yes |
| AC3 - Required fields are validated | 2 (Medium) | Ordinary validation/UX | Yes |
| AC4 - Redirect target depends on role | 4 (High) | Gates access to the admin area | Yes |
| AC5 - TOTP step for 2FA-enabled accounts | 4 (High) | Security-relevant | No - Example still provisional, no real account to run it against |
| AC6 (NEW) - Account locked after 3 failed attempts (non-admin) | 5 (Critical) | Brute-force protection; found in `UserService::login()` (`MAX_LOGIN_ATTEMPTS = 3`), not visible from the frontend component at all | No - not in the map yet |
| AC7 (NEW) - Disabled account is rejected even with the correct password | 4 (High) | Administrative access control; `$user->enabled` check in the same backend method, distinct error ("Account disabled") from AC2's | No - not in the map yet |
| Logout AC1 - Signing out ends the session | 4 (High) | Security: must terminate the session | Yes |

**Score = 10 x (5+5+2+4+0+0+0+4) / (5+5+2+4+4+5+4+4) = 10 x 20/33 = 6.1**

## Concerns
- ➕ Missing AC: account lockout after 3 failed attempts - error "Account locked, too many failed attempts. Please contact the administrator." Successful login resets the counter to 0 (`UserService::incrementLoginAttempts`/reset).
- ➕ Missing AC: disabled-account rejection - error "Account disabled", returned even when the password is correct, checked after credential match and before token issuance.
- ❓ Still open from iteration 1: no seeded account has TOTP enabled. Resolution path found this pass: no seeder sets `totp_enabled = true` for any default account, so one would need to be enabled via the account's own settings (self-service), not just picked from existing fixtures - that's a setup step for Stage 3, not a blocker for finishing the map.

## Relationships - needs a human decision
(unchanged from iteration 1 - see gate-report-1)

## Notes
(none)
