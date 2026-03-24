using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Steps;

[Binding]
public class ScreenshotSteps
{
    private readonly ScenarioContext _context;
    private IPage Page => _context.Get<IPage>();
    private string BaseUrl => _context.Get<string>("BaseUrl");

    public ScreenshotSteps(ScenarioContext context)
    {
        _context = context;
    }

    [Given(@"the viewport is ""(.*)""")]
    [When(@"the viewport is ""(.*)""")]
    public async Task GivenTheViewportIs(string resolution)
    {
        var (width, height) = resolution switch
        {
            "1080p" => (1920, 1080),
            "4K" => (3840, 2160),
            _ => throw new ArgumentException($"Unknown resolution: {resolution}")
        };

        await Page.SetViewportSizeAsync(width, height);
    }

    [Then(@"I take a screenshot named ""(.*)""")]
    public async Task ThenITakeAScreenshotNamed(string name)
    {
        var directory = Path.Combine("TestResults", "Screenshots");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{name}.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true
        });
    }
}
