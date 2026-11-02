---
name: exploration
description: Guided procedure for exploring an EXISTING, already-built and running feature (the running app plus its source code) against a provided example map, to review/extend the map and grade its coverage before any test gets written. Requires a map as input - does not improvise one. Not for designing acceptance tests for a feature that hasn't been built yet (that's a different, not-yet-written workflow).
---

# Exploration (existing features)

> DRAFT - piloting against real features before this is treated as settled.

## Scope

This skill is for characterizing a feature that **already exists and is running**. It is the first
stage of the `existing-feature-acceptance-testing` workflow. It is never used to design tests for a
feature that hasn't been built yet - there is no running app or source to explore in that case, and
a different (future) workflow owns that.

Read the `example-mapping` skill first if you haven't - this skill assumes you already know the
Feature/Story/AC/Example/Question vocabulary and format.

## Required inputs

1. **An example map**, in the `example-mapping` format, covering the Story (or Stories) in scope.
   This is provided, not written here - if none exists yet, stop and ask for one (or for a rough
   skeleton) rather than inventing the map's content yourself. The map is what you're reviewing, not
   something you own.
2. **A running local instance of the application** - never the live/production site, only the
   project's own local dev stack. Confirm it's actually reachable before starting; don't assume.
3. **The application's source code**, available for reading.

## Process

### 1. Read the map first

Before touching the app, list out everything the map already claims: every Story, every AC under
it, every Example under each AC, every open Question. This is your checklist for step 2 and your
baseline for step 2's "nothing in the map says this" check.

### 2. Walk the running app, guided by the map

UI-first, not code-first - the running app is the ground truth for what a user actually experiences;
the code explains *why*, not *what*.

For each AC in the map:
- Find the corresponding flow/page in the running app.
- Execute every existing Example by hand, exactly as written (the data in the Example, the steps
  implied by Given/When/Then). Record whether the app's actual behaviour matches what the Example
  claims.
- While you're on that flow, watch for anything the map doesn't mention: extra options, alternate
  paths, conditional fields, loading states, redirects, error states you triggered by accident.
  These are gap candidates for step 4.

Don't limit yourself to clicking through happy paths - deliberately try the failure modes a rule
implies (empty required fields, boundary-adjacent values, obviously wrong formats, resubmitting with
data that should now conflict, e.g. a duplicate email).

### 3. Cross-check against source

Now read the feature's source (template/markup + component/controller logic). This step exists to:

- **Explain** mismatches found in step 2 (why does the app behave that way?).
- **Reveal what the UI can't show you directly**: exact validation thresholds, exact error-message
  strings and which ones are client-side vs server-side, debounced/async side effects (a field
  combination silently triggering a lookup that fills in other fields), role-based branching you'd
  need a second account to see, feature flags.
- **Pin down precise wording** for any new Example you're about to propose - use the real validator
  rule and the real error copy, not an approximation.

If the application's source lives in a monorepo with multiple deployed variants (e.g. several sprint
branches of the same app), confirm which variant the running instance is actually serving (a version
string, a build date, a feature only present in one variant) before trusting what you read - don't
assume the first matching file you find is the one that's live.

**Confirm every Example through both the live app and the source where both are possible.** When
only one is actually possible (the live app can't reach the state - no seeded account exists to
exercise it, say), **use the source, not a live-only guess.** Write the Example to the
source-documented/intended behaviour and let Stage 2's eventual test assert it for real. If the live
app turns out to disagree, that test *fails* - and a failing test is a definite, confirmed finding,
not an assumption. The alternative (asserting whatever's observed live, or just noting "not yet
live-tested" as a footnote) risks quietly codifying a real bug as correct, or leaving a genuine gap
invisible until someone happens to re-check it. Found for real in this project: Registration's AC6
age-ceiling Example was written from source alone (`StoreCustomer.php`'s `after: 75 years ago` rule)
with exactly this reasoning - Stage 3's test, asserting that source-documented rule, failed live, and
that failure is what proved the rule doesn't actually work. Source-then-live-check is a strength of
this exploration process, not a fallback to apologise for in a footnote.

### 4. Tag every finding

Classify everything you found in steps 2-3 using exactly these six tags:

| Tag | Meaning |
| --- | --- |
| ✅ Confirmed | An existing Example matches real app/code behaviour as written. |
| ⚠ Mismatch | An existing Example or AC contradicts real app/code behaviour. |
| ➕ Missing Story | A whole user-facing option/path exists in the app/code with no Story representing it at all. |
| ➕ Missing AC | Within an existing Story, a distinct rule/condition exists with no AC capturing it at all (not just thin coverage of one that exists). |
| ➕ Missing Example | An AC that exists but is missing a specific case visible in code or usage (a boundary value, a particular error string, a role variant). |
| ❓ Question | Something unresolved - ambiguous, contradictory, or genuinely unknown. Don't guess; write the question. |
| 🔗 Relationship | This Story/AC touches a *different* Feature (e.g. hands off to it, shares data with it, depends on its state). Note it and what it implies for test structure (does this belong in a feature-level test, or a cross-feature journey test?). Don't chase into exploring the other Feature's own map - that's a separate pass if one's needed. |

### 3a. Data that's inherently runtime-only

Some fields can never be a fixed value in an Example - a TOTP code, a generated id, "today's date".
Don't treat this as a gap to close by hand-computing a real value during exploration (standing up a
throwaway account specifically to produce one is setup/teardown work, which belongs to the eventual
test itself, not to this read-mostly stage). The correct, *final* shape of that Example states where
the value comes from and how it's computed at runtime, with a dummy placeholder standing in for the
literal value - that counts as fully covered, not provisional. Don't confuse "this field is
necessarily dynamic" with "this AC is under-explored."

### 3b. Cross-AC/Example data consistency

Check whether a literal data value (a password, an email, a date) gets reused across more than one
AC's Examples - and if so, whether what each AC claims about that value is actually compatible.
Found for real in this project's Registration map: AC3-E3 proposed the password `Abcd123!` as proof
the length check passes, while AC7-E1 proposed the *same literal password* as proof it gets
breach-rejected - both claims are individually correct, but not simultaneously true of one value, so
no real test could ever use it for both purposes at once. This kind of conflict is invisible within
either AC's own section and only surfaces once Stage 2 tries to build real test data from the map -
catch it here instead.

Reuse itself isn't the problem - two Examples genuinely sharing a stable precondition (the same
seeded account, the same known-invalid format) is fine and keeps the map's data surface small. What
makes it a real conflict is two Examples needing contradictory things to be true of the *same*
literal value. The other hard constraint, independent of reuse: every Example must stay testable on
its own - a test built from one Example can never depend on another Example's test having run first
or left behind state, even when they happen to describe the same input. Flag a genuine value
conflict as a ❓ Question, same as any other finding - don't resolve it silently by picking one AC's
claim over the other.

### 4a. Every Question must reach a terminal state

A ❓ Question is not allowed to just sit in a report forever unexamined. Each one ends in exactly one
of:

- **Answered** - exploration resolved it (see below).
- **Disregarded** - the map's owner decided it doesn't matter / isn't in scope.
- **Postponed** - the map's owner acknowledged it's real but deferred it (e.g. to Stage 3, or to a
  later pass).

Who resolves it, and when to stop and ask, follows one rule: **if it's reasonable to expect
exploration can answer the Question itself within one more iteration, attempt that before involving
the user.** If it can't be answered by exploration at all (it's a product/business decision, or it
needs something outside exploration's own scope - e.g. standing up a fixture that's arguably setup
work, not investigation), or resolving it would take *more* than one more iteration, stop and ask the
user rather than open-endedly chasing it.

When you ask, the user may give a full answer, or just say **disregard** or **postpone** - both are
valid, closing answers, even though a full answer is preferable. Don't treat "disregard"/"postpone" as
a non-answer that leaves the Question still open; record which one it was.

### 5. Produce two separate outputs

**(a) Revised example map** - a full new copy of the input map with every change folded in:
corrected Examples, new Stories/ACs/Examples added for gaps, Questions updated (resolved ones
removed or answered, new ones added). Save it as a new file alongside the original (don't overwrite
it) so the two can be diffed. Keep this document clean - no grading, no tag markers, no "found by
exploration" annotations cluttering it. It should read exactly like a map someone sat down and wrote
well in one pass.

**(b) Gate report** - a separate, short file. Just the grade and the concerns that drove it - not a
restatement of the whole map, not a re-listing of every ✅ Confirmed item.

## The gate

The score is a **weighted coverage percentage**, not a flat deduction pile: build your own view of
what 100% of this Story/Feature's scope actually is from everything step 2-3 turned up, weight each
piece by how much it matters, and grade the *input* map against that total. The gate marks the map
down for everything it fails to include - the risk this carries is that step 2-3 itself missed
something real, which is exactly why exploration insists on both the running app and the source
rather than either alone. It is a best-effort estimate against this pass's own findings, not a
provable guarantee of completeness - say so in the report (see shape below).

### 1. Build the scope inventory

List every AC (existing in the map, or found missing in step 4) across every Story in scope -
excluding 🔗 Relationships, which never enter this calculation (see below). This list is the
denominator.

### 2. Weight each AC

Assign each AC a weight reflecting how much it matters to this feature - risk-based, not a flat
constant per tag-type:

| Weight | Guide |
| --- | --- |
| 5 - Critical | Core happy path, or a security/access/data-integrity rule. Getting this wrong is the worst outcome a test suite for this feature could fail to catch. |
| 4 - High | A rule that gates access, money, or another security-relevant branch, but isn't the central happy path. |
| 2 - Medium | Ordinary validation/UX rules - wrong input handled, format checked. |
| 1 - Low | Cosmetic or low-consequence rules. |

This is a judgement call, made during exploration, and it should be visible in the report (which AC
got which weight, briefly why) - not a hidden calculation.

### 3. Score the input map against that inventory

For each AC in the inventory: it counts toward the numerator only if the **input map** (before your
changes) captured it *and* got it right. ⚠ Mismatch and ➕ Missing both score as not-covered for that
AC's weight - a confidently wrong Example is not partial credit, even though it's a different kind of
problem to fix than an absent one (say which it was in the concerns list; don't let the score hide
that distinction).

```
score = 10 x (sum of weights of ACs the input map covered correctly) / (sum of weights of all ACs in the inventory)
```

**Bands:**

| Score | Band | Effect |
| --- | --- | --- |
| 0-4 | Hard gate | The `existing-feature-acceptance-testing` workflow stops here. The map needs revising (by its owner, using the concerns list) before Stage 2 can start. |
| 5-8 | Advisory | Workflow may proceed to Stage 2, but every concern on the list is presented for an explicit accept/reject before moving on - not silently carried forward. |
| 9-10 | Informational | Map is solid. Remaining notes (minor missing examples) are FYI only, not a decision point. |

### Relationships are a human call, not a score

🔗 Relationship findings never enter the weighted calculation - whether a given relationship belongs
in this feature's scope (an AC/Example here) or is out of scope (handled by the other feature's own
map, or by a cross-feature journey test) depends on what the relationship actually is, and that's a
judgement for the map's owner, not something to resolve algorithmically. Record each one as an open
decision in the gate report rather than silently picking include or exclude yourself.

### Gate report shape

```markdown
# Exploration Gate Report - <Feature/Story>

**Grade: <0-10> (<band>)**

Coverage is assessed against the scope found during this exploration pass - a best-effort estimate,
not a proof that nothing was missed.

## Scope inventory
| AC | Weight | Covered by input map? |
| --- | --- | --- |
| ... | 5 | No - missing |
| ... | 4 | No - mismatch |
| ... | 5 | Yes |

## Concerns
- ⚠ ...
- ➕ Missing AC: ...
- ❓ ...

## Relationships - needs a human decision
- 🔗 <relationship> - include in this map's scope, or out of scope (handled elsewhere)? <brief reasoning, no default assumed>

## Notes
- anything else informational
```

Nothing else goes in this file.
