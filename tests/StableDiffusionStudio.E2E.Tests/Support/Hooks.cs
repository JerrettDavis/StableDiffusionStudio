using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Support;

[Binding]
public class Hooks
{
    private static WebAppFixture? _webApp;
    private static PlaywrightFixture? _playwright;

    [BeforeTestRun]
    public static async Task BeforeTestRun()
    {
        _webApp = new WebAppFixture();
        await _webApp.InitializeAsync();

        _playwright = new PlaywrightFixture();
        await _playwright.InitializeAsync();
    }

    [AfterTestRun]
    public static async Task AfterTestRun()
    {
        if (_playwright != null) await _playwright.DisposeAsync();
        if (_webApp != null) await _webApp.DisposeAsync();
    }

    [BeforeScenario]
    public async Task BeforeScenario(ScenarioContext context)
    {
        var page = context.ScenarioInfo.Tags.Contains("viewport-4k")
            ? await _playwright!.NewPageAsync(3840, 2160)
            : await _playwright!.NewPageAsync();
        context.Set(page);
        context.Set(_webApp!.BaseUrl, "BaseUrl");
    }

    [AfterScenario]
    public async Task AfterScenario(ScenarioContext context)
    {
        if (context.TryGetValue<IPage>(out var page))
        {
            await page.CloseAsync();
        }
    }
}
