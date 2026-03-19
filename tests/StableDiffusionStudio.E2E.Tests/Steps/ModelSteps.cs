using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Steps;

[Binding]
public class ModelSteps
{
    private readonly ScenarioContext _context;
    private IPage Page => _context.Get<IPage>();
    private string BaseUrl => _context.Get<string>("BaseUrl");

    public ModelSteps(ScenarioContext context)
    {
        _context = context;
    }

    [Given(@"I am on the models page")]
    public async Task GivenIAmOnTheModelsPage()
    {
        await Page.GotoAsync($"{BaseUrl}/models");
        await WaitForBlazorAsync();
    }

    [When(@"I enter a valid directory path")]
    public async Task WhenIEnterAValidDirectoryPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "sds_test_models");
        Directory.CreateDirectory(tempDir);
        var dialog = Page.Locator(".mud-dialog");
        await dialog.WaitForAsync(new() { Timeout = 10_000 });
        var input = dialog.Locator("input").First;
        await input.ClickAsync();
        await input.FillAsync(tempDir);
        await input.DispatchEventAsync("change", new { });
        await Page.WaitForTimeoutAsync(300);
    }

    [When(@"I enter ""(.*)"" as the display name")]
    public async Task WhenIEnterAsTheDisplayName(string name)
    {
        var dialog = Page.Locator(".mud-dialog");
        var input = dialog.Locator("input").Nth(1);
        await input.ClickAsync();
        await input.FillAsync(name);
        await input.DispatchEventAsync("change", new { });
        await Page.WaitForTimeoutAsync(300);
    }

    [Then(@"I should see a notification about adding the directory")]
    public async Task ThenIShouldSeeANotificationAboutAddingTheDirectory()
    {
        var snackbar = Page.Locator(".mud-snackbar-content-message");
        await Expect(snackbar).ToBeVisibleAsync();
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
