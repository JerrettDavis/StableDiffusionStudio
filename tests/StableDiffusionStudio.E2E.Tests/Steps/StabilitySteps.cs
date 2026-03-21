using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Steps;

[Binding]
public class StabilitySteps
{
    private readonly ScenarioContext _context;
    private IPage Page => _context.Get<IPage>();
    private string BaseUrl => _context.Get<string>("BaseUrl");

    public StabilitySteps(ScenarioContext context) => _context = context;

    [Given(@"I am on the generate page")]
    public async Task GivenIAmOnTheGeneratePage()
    {
        await Page.GotoAsync($"{BaseUrl}/generate");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(2000); // Wait for Blazor circuit + lazy init
    }

    [When(@"I wait for the page to fully load")]
    public async Task WhenIWaitForThePageToFullyLoad()
    {
        // Wait for any loading indicators to disappear
        await Task.Delay(3000);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [Then(@"the app should be responsive")]
    public async Task ThenTheAppShouldBeResponsive()
    {
        // Verify we can still interact — click the nav menu
        var navLink = Page.Locator(".mud-nav-link").First;
        await Assertions.Expect(navLink).ToBeVisibleAsync();

        // Verify no fatal error page
        var body = await Page.ContentAsync();
        body.Should().NotContain("Fatal error");
        body.Should().NotContain("0xC000001D");
        body.Should().NotContain("ExecutionEngineException");
    }

    [Then(@"I should not see a generating spinner")]
    public async Task ThenIShouldNotSeeAGeneratingSpinner()
    {
        // The generating-pulse class indicates active generation
        var spinners = Page.Locator(".generating-pulse");
        var count = await spinners.CountAsync();

        // Also check for the "Generation in progress" text
        var progressText = Page.GetByText("Generation in progress");
        var hasProgress = await progressText.CountAsync() > 0;

        if (count > 0 || hasProgress)
        {
            // On a fresh startup after stale job cleanup, there should be no active generation
            throw new Exception("Found generating state on fresh startup — stale jobs not cleaned up");
        }
    }

    // "I should see the ... tab" step is defined in NavigationSteps — no duplicate needed
}
