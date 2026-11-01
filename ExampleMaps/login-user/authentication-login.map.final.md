# Feature: Authentication

## Story: Registered customer logs in
As a registered customer, I want to log in with my email and password, so that I can access my account.

### AC1 - Valid credentials log the user in
Given a registered customer enters their correct email and password
When they submit the login form
Then they are logged in and redirected to their account overview page

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | email=customer@practicesoftwaretesting.com, password=welcome01 | submit login form | redirected to `/account`, nav bar shows the account menu with the customer's name |

### AC2 - Invalid credentials show an error
Given a customer enters an email/password that doesn't match an account
When they submit the login form
Then an error message is shown and they stay on the login page

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | email=customer@practicesoftwaretesting.com, password=wrongpassword | submit login form | error "Invalid email or password" shown, still on `/auth/login` |

### AC3 - Required fields are validated
Given a customer submits the login form with a field left empty
When they submit
Then a required-field error is shown for that field, nothing is submitted

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | email and password both empty | submit login form | "Email is required" and "Password is required" shown |

### AC4 - Redirect target depends on role
Given an account with the admin role logs in with correct credentials
When they submit the login form
Then they are redirected to the admin dashboard instead of the account overview page

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | email=admin@practicesoftwaretesting.com, password=welcome01 | submit login form | redirected to `/admin/dashboard` |

### AC5 - Accounts with two-factor authentication enabled require a verification code
Given an account that has TOTP enabled logs in with correct credentials
When they submit the login form
Then a restricted, TOTP-only token is issued and a verification-code prompt is shown; submitting the correct code exchanges it for a full session token, submitting an incorrect code returns "Invalid TOTP" and does not sign them in

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | a throwaway account, created and TOTP-enabled as part of this test's own setup; code = *&lt;computed at runtime from the account's TOTP secret, e.g. via a Google2FA/RFC 6238-compatible library - never a fixed value&gt;* | submit login form, then submit the current, correctly-computed TOTP code | restricted token issued -> full session token issued, logged in |
| E2 | same account as E1; code = `000000` (dummy - any code that fails to match the real one demonstrates this case) | submit login form, then submit an incorrect TOTP code | "Invalid TOTP" shown, not logged in |

**Note:** unlike every other Example in this map, E1's code is never a literal value you can write down - TOTP codes rotate every 30 seconds by design, so the only correct way to populate it is computing it at the moment the test runs, from the secret captured when TOTP was enabled. This isn't a gap to close later; it's the correct, final shape of this Example. The account itself is not a shared fixture - no seeded account has TOTP enabled (`TOTPService` explicitly blocks it for a `restrictedEmails` list, which the default demo accounts are on - confirmed live: `/account/profile`'s TOTP setup returned "Access denied: If you want to configure TOTP, please create your own account." for a seeded login), so a test exercising this AC registers its own throwaway account, enables TOTP via `/account/profile` (`POST /totp/setup` for the secret, `POST /totp/verify` to enable it), runs its assertions, and removes the account afterward - the same create/use/discard pattern as any other test-owned fixture, not something exploration needs to pre-create.

**Questions:**
- ✅ Answered: where does a TOTP-enabled account/code come from, given no seeded one exists? See the note above - created and torn down per test, code computed at runtime.
- `UserService::login()` calls `resetLoginAttempts()` in the password-only success path (after a non-TOTP login), but the TOTP-verification success path doesn't visibly call it. Does a TOTP-enabled account that had some failed password attempts, then logs in correctly via TOTP, get its `failed_login_attempts` counter reset? If not, it may accumulate toward lockout (AC6) across unrelated sessions. Needs a source-level answer, not a guess - postponed to Stage 3, since answering it means reading the attempt-counter logic more closely than this pass did, not re-exploring the running app.

### AC6 - Account is locked after repeated failed attempts
Given a non-admin account has reached 3 failed login attempts
When a further login attempt is made (even with the correct password)
Then the login is rejected with "Account locked, too many failed attempts. Please contact the administrator." - admin accounts are exempt from this rule

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | a non-admin account with `failed_login_attempts = 3` | submit login form with the correct password | rejected: "Account locked, too many failed attempts. Please contact the administrator." |
| E2 | a non-admin account with `failed_login_attempts = 2`, then one more wrong-password attempt | submit login form with the correct password immediately after | rejected: the 3rd wrong attempt pushes the count to 3, so the *next* attempt (even a correct one) is locked out |
| E3 | the admin account, hypothetically at `failed_login_attempts = 3` | submit login form with the correct password | **not** locked out - the rule explicitly excludes `role == "admin"` |

A successful login resets the counter to 0 (`resetLoginAttempts`), so the lockout only triggers from
consecutive failures, not a lifetime total.

**Source:** `API/app/Services/UserService.php`, `login()` + `incrementLoginAttempts()`/`resetLoginAttempts()`, `MAX_LOGIN_ATTEMPTS = 3`. Not visible from the Angular login component at all - the frontend has no knowledge of this rule, it just renders whatever error string the backend returns.

**Verification note:** confirmed by reading the source; not yet exercised live against the running app, to avoid locking out the shared seeded `customer@practicesoftwaretesting.com` account that other Examples in this map depend on. Recommend verifying with a disposable/throwaway account (created via Registration) before Stage 3 relies on this AC.

### AC7 - Disabled accounts cannot log in, even with the correct password
Given an account has been disabled (`enabled = false`, an admin-controlled flag)
When the account holder submits the correct email and password
Then the login is rejected with "Account disabled" - distinct from AC2's "Invalid email or password", because the credentials themselves were correct

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | a disabled account, correct email/password | submit login form | rejected: "Account disabled" (not "Invalid email or password") |

**Source:** `API/app/Services/UserService.php`, `login()` - the `enabled` check runs *after* `auth()->attempt()` succeeds, so this is checked only once credentials are already known to be correct.

**Verification note:** confirmed by reading the source only - disabling an account requires an admin action (see `admin/users-add-edit`) not yet exercised here.

---

## Story: Registered customer logs out
As a registered customer, I want to log out, so that my session ends on this device.

### AC1 - Signing out ends the session and returns to the login page
Given a logged-in customer opens the account menu
When they click "Sign out"
Then their session ends and they are returned to the login page

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | logged in as customer@practicesoftwaretesting.com | open account menu -> click "Sign out" | redirected to `/auth/login` |

---

## Relationships

- 🔗 The login page's "Forgot your Password?" link hands off to the **Password Reset** feature - not explored here. If that feature has its own example map, check it covers "arrived here from Login" as a path; otherwise this is a candidate Story for that map, not this one.
- 🔗 Successful login is also where the **Registration** feature hands off *to* (registering doesn't auto-login - it redirects here). Worth a journey-level example ("register then log in with the new account") rather than duplicating it inside either feature's own map.
