# Feature: Registration

## Story: Guest creates an account
As a guest, I want to register an account with my details, so that I can log in and shop as a customer.

### AC1 - Valid details create an account
Given a guest fills in all required registration fields with valid data
When they submit the registration form
Then the account is created and they are redirected to the login page

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | valid first/last name, dob, address, phone, a unique email, a password meeting complexity rules | submit registration form | redirected to `/auth/login` |

### AC2 - Required fields are validated
Given a guest leaves a required field empty
When they submit the registration form
Then a required-field error is shown for that field

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | first name empty, everything else valid | submit | "First name is required" |
| E2 | last name empty, everything else valid | submit | "Last name is required" |
| E3 | date of birth empty, everything else valid | submit | "Date of Birth is required" |
| E4 | country not selected, everything else valid | submit | "Country is required" |
| E5 | postcode empty, everything else valid | submit | "Postcode is required" |
| E6 | house number empty, everything else valid | submit | "House number is required" |
| E7 | street empty, everything else valid | submit | "Street is required" |
| E8 | city empty, everything else valid | submit | "City is required" |
| E9 | state empty, everything else valid | submit | "State is required" |
| E10 | phone empty, everything else valid | submit | "Phone is required." |
| E11 | email empty, everything else valid | submit | "Email is required" |
| E12 | password empty, everything else valid | submit | "Password is required" |

### AC3 - Password must meet complexity rules
Given a guest enters a password that doesn't meet the complexity rules
When they submit the registration form
Then a password error is shown describing what's missing

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | password `abc` (3 chars, no mixed case/number/symbol) | submit | length error shown (displayed copy: "Password must be minimal 6 characters long." - see Question) |
| E2 | password 7 chars with correct case/number/symbol (e.g. `Abc123!`) | submit | still rejected - the real minimum is 8, not what the copy says |
| E3 | password 8+ chars with correct case/number/symbol (e.g. `Abcd123!`) | submit | length check passes |

**Questions:**
- 🚫 Disregarded: the displayed error for a too-short password says "Password must be minimal 6 characters long.", but the enforced minimum is actually 8 (confirmed: 7 chars with everything else correct still fails, 8 passes). Not being tracked as a bug. The Examples above still assert the real behaviour (8), since that's what a test must check regardless.

### AC4 - Postal code lookup fills in address fields
Given a guest enters a country, postal code and house number
When all three are valid
Then the street, city and state fields are automatically filled in

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | country=Netherlands, postal_code=1011AB, house_number=1 | fields entered | street/city/state auto-filled |

### AC5 - Duplicate email is rejected
Given a guest registers with an email that's already in use
When they submit the registration form
Then an error is shown and no account is created

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | email=customer@practicesoftwaretesting.com (already registered), everything else valid | submit | "A customer with this email address already exists." |

### AC6 - Customers must be between 18 and 75 years old
Given a guest's date of birth makes them younger than 18 or older than 75
When they submit the registration form
Then the registration is rejected with an age-specific error

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | dob makes the customer 6 years old | submit | "Customer must be 18 years old." (confirmed live) |
| E2 | dob makes the customer 80 years old | submit | "Customer must be younger than 75 years old." (source-confirmed, not yet live-tested) |

**Source:** `API/app/Http/Requests/Customer/StoreCustomer.php`, `rules()`/`messages()` - `dob` must be `before` 18 years ago and `after` 75 years ago. Not visible anywhere in the frontend (`register.component.ts` only checks ISO date *format*, not age).

### AC7 - Passwords that have appeared in a known data breach are rejected
Given a guest enters a password that meets the complexity rules but is known to have been exposed in a data breach
When they submit the registration form
Then the registration is rejected with a breach-specific error, distinct from the complexity errors

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | password `Abcd123!` (meets length/case/number/symbol, but is a common/breached pattern) | submit | "The given password has appeared in a data leak. Please choose a different password." (confirmed live) |

**Source:** `API/app/Http/Requests/Customer/StoreCustomer.php` - `Password::min(8)->mixedCase()->numbers()->symbols()->uncompromised()`. The `uncompromised()` check (Laravel's Have I Been Pwned integration) is invisible from the frontend entirely - the password-strength indicator and `PasswordValidators` only check length/case/number/symbol, never breach status.

### AC8 - Field values are length-limited
Given a guest enters a value longer than a field's allowed maximum
When they submit the registration form
Then the registration is rejected

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | first_name longer than 40 characters | submit | rejected (exact message not yet confirmed live) |
| E2 | last_name longer than 20 characters | submit | rejected (exact message not yet confirmed live) |

**Source:** `API/app/Http/Requests/Customer/StoreCustomer.php` - `max:40` (first_name), `max:20` (last_name), `max:70` (street), `max:40` (city/state/country), `max:10` (postal_code/house_number), `max:24` (phone), `max:256` (email). Not visible from the frontend, not yet live-tested.

### AC9 - Subscript/superscript characters are rejected in text fields
Given a guest enters a subscript or superscript unicode character (U+2070-U+209F) in a name, address, phone, email or password field
When they submit the registration form
Then the registration is rejected with "The {field} must not contain subscript/superscript characters."

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | first_name contains a superscript character (e.g. `Pilot⁵`) | submit | "The first_name must not contain subscript/superscript characters." (source-confirmed, not yet live-tested) |

**Source:** `API/app/Rules/SubscriptSuperscriptRule.php` - applied to first_name, last_name, every address sub-field, phone, email and password. Likely an anti-spoofing/homoglyph measure; not referenced anywhere in the frontend.

---

## Relationships

- 🔗 Successful registration redirects to the Login page rather than auto-authenticating (confirmed live - no auto-login). Same relationship already flagged from the Login side (`login-user/authentication-login.map.final.md`). Include a journey-level example here, there, or in a separate journey map - needs a human decision.
