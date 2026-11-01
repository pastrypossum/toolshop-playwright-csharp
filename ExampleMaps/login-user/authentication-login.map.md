# Feature: Authentication

## Story: Registered customer logs in
As a registered customer, I want to log in with my email and password, so that I can access my account.

### AC1 - Valid credentials log the user in
Given a registered customer enters their correct email and password
When they submit the login form
Then they are logged in and redirected to the home page

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | email=customer@practicesoftwaretesting.com, password=welcome01 | submit login form | redirected to home page, logged in |

**Questions:**
- (none)

### AC2 - Invalid credentials show an error
Given a customer enters an email/password that doesn't match an account
When they submit the login form
Then an error message is shown and they stay on the login page

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | email=customer@practicesoftwaretesting.com, password=wrongpassword | submit login form | error message shown |
