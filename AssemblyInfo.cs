// Fixtures (test files/classes) run in parallel with each other, AND a fixture's own tests run
// concurrently with each other too (ParallelScope.All), up to 8 at once by default. This applies
// to Contract tests (ApiTest-derived, no override) - UI tests (ToolshopTest-derived) override
// back to ParallelScope.None on the class itself, which NUnit honors over this assembly default
// (confirmed empirically: a ToolshopTest-derived fixture's own tests still run strictly
// sequentially even under this All default - see ToolshopTest's own doc comment for why).
[assembly: Parallelizable(ParallelScope.All)]
[assembly: LevelOfParallelism(8)]
// Load-bearing, not just a safety net, now that the default above is ParallelScope.All: without
// this, NUnit would share ONE instance of each [TestFixture] class across all of its concurrently-
// running [Test] methods, which is fatal since every test method holds its own browser Page / API
// context in instance fields (see ToolshopTest, ApiTest) - two tests sharing one Page/ApiContext
// concurrently is exactly the kind of race this assembly's own parallelism history warns about.
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
