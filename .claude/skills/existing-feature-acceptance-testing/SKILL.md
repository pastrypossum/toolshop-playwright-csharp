---
name: existing-feature-acceptance-testing
description: End-to-end workflow for adding acceptance tests for a feature that already exists and is running - explore and grade its example map, write tests structured by it, build the supporting code, then refactor. Use when asked to add or extend acceptance tests for an existing feature. Not for a feature that hasn't been built yet (new-feature/BDD-ATDD work is a separate, not-yet-written workflow).
---

# Existing-Feature Acceptance Testing Workflow

> DRAFT - piloting against real features before this is treated as settled.

## Scope

Four stages. This skill only sequences them and connects their outputs together - it does not
duplicate the detailed conventions already owned by other skills in this project (`page-object`,
`test`, `test-data`, `allure`, `api-helper`). Those are followed as-is in Stages 2-3; this skill
doesn't restate them.

This workflow is only for features that already exist and are running. If the feature hasn't been
built yet, stop - this isn't the right skill.

## Stage 1 - Exploration

Run the `exploration` skill (which itself depends on `example-mapping`). Requires an example map as
input, provided by whoever owns the feature's scope - don't proceed into this stage without one.

Output: a revised map + a gate report (grade 0-10 + concerns).

**Loop on the gate band:**

- **0-4 (hard gate):** Stop. Report the concerns. The map's owner revises it (using the concerns as
  a punch list), and Stage 1 runs again against the revised map. Do not proceed to Stage 2.
- **5-8 (advisory):** Present every concern for an explicit accept/reject before moving on. Carry
  accepted gaps forward into Stage 2 as known scope, not as silent omissions.
- **9-10 (informational):** Proceed. Surface the notes (e.g. 🔗 relationships) but don't block on
  them.

## Stage 2 - Add acceptance tests

Run the `writing-acceptance-tests` skill: turns the finalized map into real test files - directory
structure, parameterization, Given/When/Then, assertions, reuse-before-writing-new, Allure, and any
test data. That skill owns all of this stage's detail; don't duplicate it here.

A 🔗 Relationship finding from Stage 1 decides where a test belongs - a human call (per the
`exploration` skill), with three real outcomes, not two:

1. **It's the natural confirming assertion of one feature's own AC.** Example: Registration's "a
   successful registration produces a working account" AC is proven by actually logging in with
   the new credentials - that login *is* the assertion, not a duplicate test or a side trip. This
   feature's Stage 3 reuses the other feature's page objects/common actions directly (e.g.
   Registration reusing Login's `LoginPage`) rather than inventing its own. No separate test, no
   journey folder - the relationship was never really "cross-feature" work, just one feature's own
   proof happening to pass through another feature's screen.
2. **It's genuinely a separate, cross-feature scenario** neither feature's own ACs cover on their
   own (e.g. "a customer abandons checkout, comes back later, and the cart is still there" - not
   really about login or checkout alone). This belongs in a journey-style test grouping - check
   whether this project already has a convention for that (e.g. a `journey/` directory) before
   inventing one.
3. **It's out of scope** for this pass - noted, not built.

Don't default to (2) just because a Relationship exists - check (1) first; a relationship that's
really just where one feature's own confirming assertion happens to live is the common case, not
the exception.

## Stage 3 - Supporting code

Build whatever Stage 2's tests need to actually run: page objects, data factories, API helpers,
common actions (composite calls spanning more than one page object/API helper), utilities (pure
helper logic with no app dependency), and fixture-base-class additions. Follow this project's
`page-object`, `test-data`, `api-helper`, `common-actions`, `utilities` and `fixtures` skills - each
owns its own slice of this stage's detail; don't duplicate it here.

Implement and verify one test at a time - write the data it needs, the page object members it
needs, then run *that single test* immediately and confirm it passes before starting the next one.
Don't implement several tests' worth of supporting code unverified; that's how a shared helper ends
up accidentally wrong for every test that used it.

## Commit boundary across Stages 2-3

Stage 2 intentionally produces code that doesn't compile yet (see "Coherence over compiling" in
`writing-acceptance-tests.instructions.md`). Checked against this project's actual CI trigger
(`.github/workflows/playwright.yml`: `push`/`pull_request` on `main`/`master` only) the real gate is
narrower than "never push":

- Commit and push a feature branch freely through Stages 2 and 3 - CI doesn't run against a branch
  with no PR open on it, so a red build there costs nothing.
- **Don't open (or update) a PR against `main`/`master`, and don't push directly to `main`/`master`,
  until the build is green** - that's the only point this project's CI actually runs build+test, and
  a red build from an intentionally-incomplete Stage 2 would fail it for a reason that has nothing to
  do with a real regression.
- If Stage 2 and Stage 3 end up spanning more than one sitting, the branch can sit there unpushed-to-
  PR for as long as it needs to - there's no deadline forcing a PR before the gap is closed.
- Stage 4 (Refactor) runs after the branch is already PR-able; keep it green throughout (small
  commits, rerun the suite after each) the same way any refactor would, regardless of this workflow.

## Stage 4 - Refactor

Once every test from Stage 2 is green.

### Goal

Readable, reusable, simple, and stable - in that priority - over complex and flaky. Every review
item below and every fix made under it serves this, not the reverse: a change that makes the code
more "correct" by some abstract measure (an extra layer of indirection for a SOLID principle, a
generalization nothing yet needs) but doesn't make it easier to read, doesn't get reused, or adds
fragility isn't a fix - it's scope creep wearing a refactor's clothes. When a review item and this
goal pull in different directions, the goal wins. Concretely: prefer one obvious page-object method
over a clever parameterized one nobody asked for; prefer a duplicated three-liner over a shared
helper built for a single caller; prefer a slower-but-reliable wait over a faster-but-racy one every
time.

A **corrective review**, not a report-and-wait gate like
Stage 1's: find an issue, fix it, confirm the suite still passes - then move to the next one. Don't
separate "found" from "fixed" the way exploration's gate report does (which exists precisely to let
a human accept/reject before anything changes) - this stage's findings are inside the feature's own
tests and supporting code just built in Stages 2-3, so fixing them directly is the job, not a list
handed back to a human. A finding that reaches outside this feature's own code (a different
feature's file, a project-wide convention question) is the exception - say so and stop rather than
fixing it silently.

### What to review

1. **Test structure** - against `writing-acceptance-tests`' own rules (Feature=folder, Story=file,
   AC grouping, parameterize-only-when-the-rule-allows). Stage 2/3 happened one test at a time; this
   is the first point anyone looks at the finished file as a whole.
2. **Assertion standard** - audit every assertion actually written against Section 4 of
   `writing-acceptance-tests` (tangible/user-visible, never URL-only as the confirming proof). Don't
   trust it was right the first time - check it.
3. **Underlying code / SOLID**, adapted to this codebase's style rather than applied textbook-literal:
   - **SRP and DIP matter most** - does each page object/common action/helper have one clear job,
     and do its dependencies arrive via constructor (the established pattern) rather than being
     reached for globally?
   - **OCP/LSP/ISP are lower-value** here, given a flat, interface-light style - worth a pass, but
     don't manufacture findings to fill the category, and never introduce an interface/abstraction
     layer to satisfy one of these in the abstract - that's exactly the complexity the Goal above
     rules out.
   - A deliberate, already-documented tradeoff (e.g. `APIHelper` being one class for every API
     concern, per its own skill, mirroring the Java project) is not a finding - don't flag an
     accepted design decision as a defect.
4. **Duplication** across the tests/page objects/supporting code written one-at-a-time in Stage 3 -
   but only once a second real caller exists (the same promote-on-second-consumer rule `test-data`
   and `common-actions` already use). Two call sites that happen to look similar today aren't
   automatically a shared helper tomorrow - a duplicated three-liner is simpler and more readable
   than an abstraction built to serve a single caller plus a guess at a future one.
5. **Test-data lifecycle** - every throwaway account/record actually gets torn down and nothing
   leaks run over run. Verify this for real (check the actual state - an admin user list, a DB
   query - not just that the `[TearDown]` code exists and looks right).
6. **Locator-convention consistency** - per `page-object`'s current locator priority. Decide
   per-feature whether older locators in files this feature touches get reconciled now or are left
   for their own feature's pass - don't silently rewrite locators outside this feature's own pages
   without saying so.
7. **No leftover aspirational code** - confirm "Coherence over compiling" fully closed: no stub
   methods, no `// TODO: Stage 3` comments, nothing still named as if it doesn't exist yet.
8. **Allure reporting quality** - steps nest correctly and read as a narrative in a real report, not
   just "an attribute is present somewhere."
9. **Map traceability** - every Example from Stage 1's final map maps to a test; every test traces
   back to an Example. Anything orphaned on either side is worth a second look before finishing.
10. **Known gaps are current** - cross-check `ExampleMaps/known-gaps.md` against this feature's own
    gap entries (if any): the covering test still exists and is still red for the stated reason, not
    silently skipped or quietly turned green by a change elsewhere. `known-gaps.md` is also something
    worth re-checking on its own, independent of any particular feature's Stage 4 pass, since a gap
    can get fixed by work that has nothing to do with the feature that found it.

### Process

Fix issues one at a time: a focused change, then re-run the specific test(s) it touches before
moving to the next issue - don't batch several fixes and re-run once at the end, which makes it hard
to tell which fix (if any) broke something else.

Once every issue from the review pass is fixed, re-run the full feature's test set multiple times
(not once) as final confirmation - check this project's own `AGENTS.md`/`CLAUDE.md` for the bar it
already uses elsewhere (e.g. "three consecutive clean runs") rather than inventing a new one.

### Output

No separate report file - Stage 4 is the last step of the workflow for this feature, not a
deliverable of its own. Summarize what was found and fixed when done; a finding that was deliberately
left alone (out of scope, a judgement call) gets called out explicitly, not silently dropped.
