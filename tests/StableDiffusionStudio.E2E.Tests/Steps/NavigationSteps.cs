using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Steps;

[Binding]
public class NavigationSteps
{
    private readonly ScenarioContext _context;
    private IPage Page => _context.Get<IPage>();
    private string BaseUrl => _context.Get<string>("BaseUrl");

    public NavigationSteps(ScenarioContext context)
    {
        _context = context;
    }

    [Given(@"I am on the home page")]
    public async Task GivenIAmOnTheHomePage()
    {
        await Page.GotoAsync(BaseUrl);
        await WaitForBlazorAsync();
    }

    [When(@"I navigate to the projects page")]
    public async Task WhenINavigateToTheProjectsPage()
    {
        await Page.Locator(".mud-nav-link", new() { HasText = "Projects" }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [When(@"I navigate to the models page")]
    public async Task WhenINavigateToTheModelsPage()
    {
        await Page.Locator(".mud-nav-link", new() { HasText = "Models" }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [When(@"I navigate to the jobs page")]
    public async Task WhenINavigateToTheJobsPage()
    {
        await Page.Locator(".mud-nav-link", new() { HasText = "Jobs" }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [When(@"I navigate to the settings page")]
    public async Task WhenINavigateToTheSettingsPage()
    {
        await Page.Locator(".mud-nav-link", new() { HasText = "Settings" }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [When(@"I navigate to the parameter lab page")]
    public async Task WhenINavigateToTheParameterLabPage()
    {
        await Page.Locator(".mud-nav-link", new() { HasText = "Parameter Lab" }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then(@"I should see the ""(.*)"" heading")]
    public async Task ThenIShouldSeeTheHeading(string heading)
    {
        var element = Page.GetByText(heading, new() { Exact = true }).First;
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see the ""(.*)"" section")]
    public async Task ThenIShouldSeeTheSection(string sectionText)
    {
        var element = Page.GetByText(sectionText).First;
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see the ""(.*)"" tab")]
    public async Task ThenIShouldSeeTheTab(string tabText)
    {
        var element = Page.GetByText(tabText).First;
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see a ""(.*)"" button")]
    public async Task ThenIShouldSeeAButton(string buttonText)
    {
        var element = Page.GetByRole(AriaRole.Button, new() { Name = buttonText });
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see a ""(.*)"" link")]
    public async Task ThenIShouldSeeALink(string linkText)
    {
        var element = Page.GetByRole(AriaRole.Link, new() { Name = linkText });
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see navigation links for ""(.*)"", ""(.*)"", ""(.*)"", ""(.*)"", and ""(.*)""")]
    public async Task ThenIShouldSeeNavigationLinks(
        string link1, string link2, string link3, string link4, string link5)
    {
        foreach (var linkText in new[] { link1, link2, link3, link4, link5 })
        {
            var link = Page.Locator(".mud-nav-link", new() { HasText = linkText }).First;
            await Expect(link).ToBeVisibleAsync();
        }
    }

    /// <summary>
    /// Waits for Blazor Server circuit to be fully connected.
    /// </summary>
    private async Task WaitForBlazorAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForFunctionAsync(
            "() => window.Blazor && window.Blazor._internal !== undefined",
            null,
            new() { Timeout = 15_000 });
        await Page.WaitForTimeoutAsync(500);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);
}
