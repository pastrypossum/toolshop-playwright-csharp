using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Fixtures;

/// <summary>
/// Base class for all UI tests in this project.
///
/// Mirrors the conventions of the Java (<c>ChromeHeadlessOptions</c>) and TypeScript
/// (<c>playwright.config.ts</c>) sibling projects:
///  - The app under test uses the <c>data-test</c> attribute (not Playwright's default
///    <c>data-testid</c>) for test ids, so it is configured on every browser context.
///  - The base URL is read from the <c>BASE_URL</c> environment variable, defaulting to
///    the live UAT site, so the same tests can target a local dev server in CI.
///
/// Derive page tests from this class (instead of <see cref="PageTest"/> directly) to get
/// both behaviours automatically.
///
/// Overrides the assembly's <c>[Parallelizable(ParallelScope.Fixtures)]</c> back to fully
/// sequential. Measured, not assumed: even just two UI fixtures running concurrently (one
/// browser each - all that exist today) was still unreliable across repeated runs (2 of 3
/// had a genuine TimeoutException, a different test each time - not a logic bug, the local
/// Angular dev server/machine couldn't reliably sustain two simultaneous real browser
/// sessions). Contract tests (<see cref="ApiTest"/>) are cheap HTTP calls with no such
/// constraint and get the full file-level parallelism; this override is scoped to UI tests
/// only.
/// </summary>
[Parallelizable(ParallelScope.None)]
public class ToolshopTest : PageTest
{
    /// <summary>
    /// Base URL for UI navigation. Override with the <c>BASE_URL</c> environment variable,
    /// e.g. <c>BASE_URL=http://localhost:4200</c> for a local dev server.
    /// </summary>
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("BASE_URL") ?? "https://practicesoftwaretesting.com";

    /// <summary>
    /// Base URL for API calls. Defaults to the local dev stack (<c>docker-compose up</c>'s
    /// <c>web</c> service) rather than the public live demo, so contract tests exercise and
    /// mutate a disposable local database by default - override with the <c>API_URL</c>
    /// environment variable to point at the live site or anywhere else instead.
    /// </summary>
    public static string ApiUrl =>
        Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:8091";

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            BaseURL = BaseUrl,
        };
    }

    [SetUp]
    public void ConfigureTestIdAttribute()
    {
        // The app renders data-test="..." attributes (not Playwright's default data-testid),
        // so GetByTestId() needs to be told which attribute to key off.
        Playwright.Selectors.SetTestIdAttribute("data-test");
    }

    /// <summary>
    /// For UI tests that also need <see cref="APIHelper"/> for setup/teardown (see
    /// <c>api-helper.instructions.md</c>). Duplicates <see cref="ApiTest"/>'s own pair rather than
    /// sharing a base - see <c>fixtures.instructions.md</c> for why.
    /// </summary>
    protected IAPIRequestContext ApiContext { get; private set; } = null!;

    [SetUp]
    public async Task CreateApiContextAsync()
    {
        ApiContext = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = ApiUrl,
        });
    }

    [TearDown]
    public async Task DisposeApiContextAsync()
    {
        await ApiContext.DisposeAsync();
    }
}
