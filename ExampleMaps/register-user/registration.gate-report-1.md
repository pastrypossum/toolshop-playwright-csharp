# Exploration Gate Report (Iteration 1) - Registration / Guest creates an account

**Grade: 7.1 / 10 (Advisory)**

Coverage is assessed against the scope found during this exploration pass - a best-effort estimate,
not a proof that nothing was missed. Grading `registration.map.md` against the inventory below.

## Scope inventory

| AC | Weight | Why this weight | Covered by input map? |
| --- | --- | --- | --- |
| AC1 - Valid details create an account | 5 (Critical) | Core happy path | Yes |
| AC2 - Required fields are validated | 2 (Medium) | Ordinary validation/UX | Yes (mechanism correct, but see concern below) |
| AC3 - Password must meet complexity rules | 4 (High) | Security-relevant | Yes (direction correct, copy imprecise - see concern) |
| AC4 - Postal code lookup fills address fields | 2 (Medium) | UX convenience, not security/access | Yes |
| AC5 - Duplicate email is rejected | 4 (High) | Data-integrity / account-uniqueness | Yes |
| AC6 (NEW) - Age restriction: customer must be 18-75 | 2 (Medium) | Business rule, not security-critical | No - not in the map at all |
| AC7 (NEW) - Password is rejected if it's appeared in a data breach | 4 (High) | Security-relevant, confirmed live | No - not in the map at all |
| AC8 (NEW) - Field length limits are enforced server-side | 1 (Low) | Minor robustness validation, not yet live-tested | No - not in the map at all |

**Score = 10 x (5+2+4+2+4) / (5+2+4+2+4+2+4+1) = 10 x 17/24 = 7.1**

## Concerns
- ➕ Missing Example: AC2's only Example covers `first_name`; the running app shows 11 distinct required-field messages (first name, last name, DOB, country, postcode, house number, street, city, state, phone, email, password), each worth its own Example, not just one.
- ❓ The password-length error visibly says "Password must be minimal 6 characters long." but the enforced minimum is actually 8 - confirmed live: a 7-character password with correct case/number/symbol still gets this error, an 8-character one does not. Is the copy wrong, or is there a separate 6-char code path not found yet? Tests should assert the real behaviour (8) regardless - this looks like a product bug worth its own ticket, not something to silently paper over in the map.
- ➕ Missing AC: age restriction. Confirmed live - a DOB making the customer 6 years old was rejected with "Customer must be 18 years old." Source (`StoreCustomer.php`) also defines the upper bound: "Customer must be younger than 75 years old." (not yet live-tested).
- ➕ Missing AC: password breach-check. Confirmed live - password `Abcd123!` was rejected with "The given password has appeared in a data leak. Please choose a different password." This is Laravel's `Password::uncompromised()` rule (`StoreCustomer.php`), invisible from the frontend's own complexity checks (length/case/number/symbol only) and from the password-strength indicator.
- ➕ Missing AC: server-side field length limits (`first_name` max 40, `last_name` max 20, `street` max 70, etc. - `StoreCustomer.php`). Source-confirmed only, not yet live-tested.
- ❓ `StoreCustomer.php` applies a custom `SubscriptSuperscriptRule` to most text fields (name, address, phone, email, password). What does it actually reject (unicode subscript/superscript characters, presumably) and what's the user-facing error copy? Not yet investigated.

## Relationships - needs a human decision
- 🔗 Successful registration redirects to the Login page rather than auto-authenticating (confirmed: no auto-login). This is the same relationship already flagged from the other side in `login-user/authentication-login.map.final.md`. Include a journey-level example here, there, or in a separate journey map - your call.

## Notes
(none)
