// Fixtures (test files/classes) run in parallel with each other, up to 8 at once; the tests
// within a single fixture run sequentially, one file at a time, start to finish - not
// ParallelScope.All, which would also run a fixture's own tests concurrently with each other.
[assembly: Parallelizable(ParallelScope.Fixtures)]
[assembly: LevelOfParallelism(12)]
// Harmless under ParallelScope.Fixtures (tests within a fixture already don't overlap), but
// kept as a safety net: without it, NUnit shares ONE instance of each [TestFixture] class
// across all of its [Test] methods, which would be fatal if a fixture's scope is ever widened
// to include Children later, since every test method holds its own browser Page / API context
// in instance fields (see ToolshopTest, ApiTest).
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
