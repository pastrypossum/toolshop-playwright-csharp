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

A 🔗 Relationship finding from Stage 1 decides where a test belongs: if it's genuinely cross-feature,
it may belong in a journey-style test grouping instead of (or in addition to) the single feature's
test file - check whether this project already has a convention for that (e.g. a `journey/`
directory) before inventing one.

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

Once every test from Stage 2 is green:

- Remove duplication introduced across the tests/page objects written one-at-a-time in Stage 3.
- Confirm Allure annotations/grouping are complete and correct per the `allure` skill.
- Re-run the full set multiple times (not once) before calling it done, if this project has
  parallelism/flakiness rules - check its own `AGENTS.md`/`CLAUDE.md` for the bar it already uses
  elsewhere (e.g. "three consecutive clean runs").
- Re-check the revised map from Stage 1 against what actually got implemented - any Example that
  didn't make it into a test, or any test that doesn't trace back to an Example, is worth a second
  look before finishing.
