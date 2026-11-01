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
| E1 | first name left empty, everything else valid | submit registration form | "First name is required" shown |

### AC3 - Password must meet complexity rules
Given a guest enters a password that doesn't meet the complexity rules
When they submit the registration form
Then a password error is shown describing what's missing

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | password `abc` (too short, no mixed case/number/symbol) | submit registration form | password error shown |

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
| E1 | email=customer@practicesoftwaretesting.com (already registered), everything else valid | submit registration form | error shown, registration rejected |
