---
name: example-mapping
description: Vocabulary, format and quality bar for an Example Map (Feature -> Story -> AC/Rule -> Example -> Question). Use whenever reading, writing, critiquing or extending an example map, in any acceptance-testing workflow - existing-feature exploration or new-feature BDD/ATDD. Does not itself drive exploration of an app or codebase.
---

# Example Mapping

> DRAFT - piloting against real features before this is treated as settled.

## What this is

Example Mapping (Matt Wynne / Cucumber) is a technique for scoping a piece of work by breaking it
into four kinds of card:

| Level | Colour (classic) | What it is |
| --- | --- | --- |
| Feature | Yellow | A capability area. Big enough to contain several Stories, small enough to fit in one map. |
| Story | Blue | One user-facing goal within the Feature. The thing a person actually came to do. |
| AC / Rule | — | One specific, independently-testable rule that must hold for the Story. |
| Example | Green | One concrete instantiation of a Rule - real data, real expected outcome. |
| Question | Red | Something unresolved. Flagged, not guessed at. |

A map is not a test plan and not a spec. It is the scoping artifact both live upstream of: it decides
what's in and out before anyone writes a test, and it gives the eventual tests their structure (one
Example tends to become one test case, one AC tends to become one `[AllureFeature]`/`[AllureStory]`
or test-fixture grouping).

This skill only covers the shape and quality of the map itself. It says nothing about *how* the
content gets discovered - that's the `exploration` skill (existing features) or the future
new-feature BDD/ATDD workflow (not yet written). Both of those produce/consume maps in this format;
neither owns the format.

## The four levels, in detail

### Feature

A capability area, not an epic and not the whole app. "User Registration" is a Feature. "The
Toolshop app" is not (too big). "Validate the postal code field" is not (too small - that's a Rule).

### Story

One user-facing goal inside the Feature. Classic phrasing: *As a \<role\>, I want \<goal\>, so that
\<benefit\>.* A Story is the right size if you can describe what "done" looks like for it in one
sentence. If a Story needs "and" to describe its goal, it's probably two Stories.

### AC / Rule

A single, independently-testable rule. "Password must be at least 8 characters, contain mixed case,
a number and a symbol" is **four** rules bundled into one sentence by the product copy - each one
can be true or false independently of the others, so each gets its own AC, not one AC with four
Examples that each happen to test a different clause.

Test for whether something is really one AC: can you imagine an Example that makes this AC pass
while a *different*, already-written AC for the same Story fails? If yes, they're already separate.
If changing the wording of this AC would force you to rewrite a sibling AC, they were never
independent and should be merged.

### Example

One concrete case: real input data, real expected outcome. Not a restatement of the AC with the
word "various" in it.

- Bad: "Invalid passwords are rejected."
- Good: "Password `abc` (7 chars, no symbol) -> rejected, error 'Password must be at least 8
  characters'."

Every AC needs at least one positive Example (the rule holds) and, where the rule can be violated,
at least one negative Example (the rule is broken and the system says so). Boundary values get their
own Example, not a footnote on another one - "exactly 8 characters" and "exactly 7 characters" are
two Examples, not one Example with a parenthetical.

### Question

Something you don't know yet and shouldn't guess. "What happens if the postcode lookup times out?"
is a Question. Don't silently resolve it by picking an assumption and writing an Example as if it
were confirmed - write the Question, leave the Example out (or mark it provisional), and let whoever
owns the map resolve it.

A Question that turns out to matter becomes either a new AC (if the answer is a rule) or gets deleted
(if the answer is "not a real concern"). A Question never just sits there forever without someone
having looked at it.

## Template

```markdown
# Feature: <name>

## Story: <name>
As a <role>, I want <goal>, so that <benefit>.

### AC1 - <short title>
<one-sentence rule statement>

**Examples:**

| # | Given | When | Then |
| --- | --- | --- | --- |
| E1 | ... | ... | ... |

**Questions:**
- ...

### AC2 - <short title>
...
```

Keep the table columns to what the Story actually needs - a simple form might only need
Given/Then; a multi-step flow might need Given/When/Then/And. Consistency within one map matters
more than matching this template exactly.

## Anti-patterns

- An Example with no concrete data ("a valid user registers successfully") - if you can't tell what
  was typed into which field, it's not an Example yet.
- An AC that's actually a Story ("user can complete checkout") - too big, break it down.
- A Story that's actually the whole Feature.
- A Question answered by guessing instead of being asked.
- Examples that only ever cover the happy path for a Story that clearly has failure modes (a
  register/login/payment Story with zero negative Examples is almost certainly incomplete, not
  simple).
