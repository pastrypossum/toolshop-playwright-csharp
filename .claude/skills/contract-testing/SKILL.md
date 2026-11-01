---
name: contract-testing
description: When API contract tests are being added, updated or reviewed.
---

## Overview

Tests that focus on the API interface by checking each side in isolation against a shared contract
 - The request a consumer makes
 - The response a server replies

## For systems using

 - microservices with independently deployed services
 - applications with separate frontend and backend teams
 - applications that use queue / event integrations
 - applications where teams cannot coordinate every release
 - applications where a shared e2e environment is slow or flaky
 - applications that need compatibility (fwd/bwd) between API versions

## Limitations

A passing a contract test only proves the structure of a request, a response or their compatibility
It does not prove correct behaviour of the API to solve a business requirement
This is in the solution domain not the problem domain

## Out of scope

A contract test asserts what crosses the wire - the request sent, the response received, its shape and status - and nothing that happens afterward on either side.

Out of scope: business logic - what a provider decides internally once its response is already contract-valid, or what a consumer does with a response after receiving it (rendering, internal state, error transformation). None of that re-crosses the wire, so a contract test has no way to see it and should not try to. Found something real but out of scope: note it (a doc comment, a flagged finding), do not write a test for it.

In scope: a value that genuinely crosses the wire twice - taken from one response and sent in a later request (a cross-endpoint reference), or a response's shape/status that depends on an earlier one (auth, versioning, pagination). Still a wire-level observation, not an assertion about what either side does with it internally.

## Concepts

Contract:                        A shared agreement between parties
Consumer:                        Client side party makes requests receives a response
Provider:                        Server side party that receives a request and provides responses
Consumer-driven contract:        Aggregate of consumer needs (sub sets) will drive what the provider must deliver (super set)
Provider states:                 Seed a provider to make meaningful verification
Matchers:                        Assert the shape of the request / response instead of the values
Broker:                          Store of versioned contracts and verification results

## Approaches

Spec-based:              Provider's own published contract (OpenAPI/Swagger) is the source of truth. No consumer involved.
Consumer-driven (Pact):  Consumer's real usage is the source of truth. Needs consumer-side contract generation, provider verification, usually a broker.

Not mutually exclusive - spec-based as a cheap baseline everywhere, consumer-driven layered on top for known, tightly-coupled consumers where it's worth the extra infrastructure.

## This project

Approach:   Spec-based baseline, layered with recorded consumer examples on top.
Recording:  Real request/response captured with `playwright-cli` against the live running app - not hand-written, not Pact.
Pact:       Not in use. Would need the consumer (a separate repo) instrumented to generate real pacts - not justified here yet.

## Key Implementation Directives

Tests must be independent and order-free: safe to run in any order, in parallel, or alone. No test may depend on another test's state, on being run before/after a specific test, or on being the only thing mutating shared data at that moment. Own data: a test that creates, updates or deletes anything creates it itself and cleans it up itself - never create "setup" data in one test for another test to consume. Own instance state: never rely on a `[SetUp]`/`[TearDown]` field being untouched by another concurrently-running test - see the parallel-execution note in `AGENTS.md`/`CLAUDE.md` for how this project avoids fixture-instance sharing.

Default to the shared fixtures, setup, teardown and login already in the project (`Fixtures/ApiTest.cs`, `Fixtures/APIHelper.cs`, and the resource's own `{Resource}ContractTestBase` if one exists) - don't write bespoke setup unless the shared helpers genuinely can't produce what the test needs.

Specific/bespoke implementation is required when:
 - the scenario needs an entity or relationship the shared fixtures don't create (e.g. a dependent child resource, to test a "resource is in use" conflict).
 - the test is deliberately exercising a bad/incomplete/malformed request (missing fields, wrong auth, invalid data) - inline the one-off payload, don't build a reusable "invalid X" helper for a single test.
 - asserting the result needs that resource's own schema - the fixture's job is to produce state, not to know what the test asserts.

If a second test needs the same bespoke setup a first one already added, that's the signal to promote it into the shared base - not before.

## Process to follow for contract testing

Spec first, Pact second - Pact only makes sense once the spec baseline exists, since it's the layer that covers what the spec can't.

### 1. Explore the endpoints in scope (shared, do this first)

 - Use Swagger - fetch its raw JSON directly rather than browsing the UI by hand, faster and more exact.
 - Check the running endpoint, and note where it disagrees with the docs.
 - Find related endpoints that help with test setup.

### 2. Spec track (`{Resource}SpecTests.cs`)

One file per resource, covering every operation on it (list/get/search/create/update/delete/...) - not one file per operation. `BrandSpecTests.cs`, not `GetBrandsSpecTests.cs`/`CreateBrandSpecTests.cs`/etc. The one exception: a test deliberately tracking a known gap (asserts the behaviour *should* be, currently fails) belongs in its own `{Resource}{Gap}SpecTests.cs` file, not mixed into the main one - a resource's main spec file should be all-green or all-meaningful-red, never both at once.

 - Write positive scenarios (200, 201 ...): define the request and response for each, cover structure, input validation and error handling.
 - Write negative scenarios (400, 401, 403, 404 ...): same, for each documented error response.
 - Error shape, not just error code: if the API has a consistent error envelope, assert its shape too - a status code alone isn't the whole contract.
 - Auth boundary: for any endpoint that requires auth, assert the unauthenticated/unauthorized response (401 vs 403) and its shape, not just the happy path.
 - Cover other versions: same approach, add only the tests that differ.

### 3. Pact track (`{Resource}PactTests.cs`)

Same rule - one file per resource, covering every consumer dependency found for it, not one file per dependency.

 - Find the real consumer. If the frontend source is available: grep it for the endpoint's wrapping service, then trace actual call sites - fastest way to find cross-endpoint/branching dependencies. If it isn't: `playwright-cli` against the live app is the primary way to find consumers at all, not just to capture - drive the real flows and see which calls actually happen.
 - Capture the real interaction: drive the live app with `playwright-cli`, use `requests` to find the call, `request <id>` + `response-body <id>` for the real headers/body. Record it as a fixture, not hand-written.
 - Trace what the consumer does with the response, not just that it receives it - this is where the valuable tests come from, not from the endpoint in isolation. With source, read it directly; without it, this means replaying the flow (e.g. following the id into whatever request comes next) rather than reading code. Look for:
   - Cross-endpoint reference: an id/value taken from this response and sent to another endpoint.
   - Client-side branching: a field the consumer uses in an if/switch to change behaviour (a status enum, a boolean flag).
   - Pagination/list-loading contract: fields a "load more"/infinite-scroll consumer depends on (cursor, has_more, total count).
 - Write tests only for the dependencies found this way - don't re-prove what the spec track already covers.
