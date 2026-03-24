using Microsoft.Playwright;

namespace StableDiffusionStudio.E2E.Tests.Support;

/// <summary>
/// Manages the Playwright browser lifecycle for E2E tests.
/// Creates a single browser instance shared across all scenarios.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task<IPage> NewPageAsync()
    {
        return await NewPageAsync(1920, 1080);
    }

    public async Task<IPage> NewPageAsync(int width, int height)
    {
        var context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new() { Width = width, Height = height }
        });
        return await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }
}
