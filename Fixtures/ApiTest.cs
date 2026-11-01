using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Toolshop.Playwright.Tests.Fixtures;

/// <summary>
/// Base class for API-only contract tests. Unlike <see cref="ToolshopTest"/>, no browser or
/// page is launched — just an <see cref="ApiContext"/> bound to the same <c>API_URL</c>-driven
/// base URL the UI tests use, for exercising the API's HTTP contract directly.
/// </summary>
public class ApiTest : PlaywrightTest
{
    protected IAPIRequestContext ApiContext { get; private set; } = null!;

    [SetUp]
    public async Task CreateApiContextAsync()
    {
        ApiContext = await Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = ToolshopTest.ApiUrl,
        });
    }

    [TearDown]
    public async Task DisposeApiContextAsync()
    {
        await ApiContext.DisposeAsync();
    }
}
