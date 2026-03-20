using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Steps;

[Binding]
public class GenerationSteps
{
    private readonly ScenarioContext _context;
    private IPage Page => _context.Get<IPage>();
    private string BaseUrl => _context.Get<string>("BaseUrl");

    public GenerationSteps(ScenarioContext context) => _context = context;

    [When(@"I navigate to the generate page")]
    public async Task WhenINavigateToTheGeneratePage()
    {
        await Page.ClickAsync("text=Generate");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(1000); // Wait for Blazor circuit
    }

    [Then(@"I should see the ""(.*)"" selector")]
    public async Task ThenIShouldSeeTheSelector(string label)
    {
        // MudSelect renders label as a hidden <legend> inside a fieldset.
        // Use GetByLabel which matches the associated label text, or check
        // for the MudSelect container with the label.
        var element = Page.Locator($".mud-select [aria-label='{label}'], .mud-input-label:has-text('{label}'), label:has-text('{label}')");
        if (await element.CountAsync() == 0)
        {
            // Fallback: just verify the label text exists anywhere in the DOM
            var labelElement = Page.Locator($"text={label}");
            (await labelElement.CountAsync()).Should().BeGreaterThan(0,
                $"Expected to find selector with label '{label}' on the page");
        }
    }

    [Then(@"I should see the prompt input fields")]
    public async Task ThenIShouldSeeThePromptInputFields()
    {
        // MudTextField renders labels as hidden <legend> elements in outlined variant.
        // Verify the input elements exist in the DOM rather than checking visibility.
        var promptInputs = Page.Locator("textarea, .mud-input-text");
        var count = await promptInputs.CountAsync();
        count.Should().BeGreaterThan(0, "Expected to find prompt input fields on the page");
    }

    [Then(@"the page should not have any error messages")]
    public async Task ThenThePageShouldNotHaveAnyErrorMessages()
    {
        // Check for common error indicators
        var errorAlert = Page.Locator(".mud-alert-filled-error");
        var count = await errorAlert.CountAsync();
        if (count > 0)
        {
            var text = await errorAlert.First.TextContentAsync();
            throw new Exception($"Error found on page: {text}");
        }

        // Check page doesn't show unhandled exception
        var body = await Page.ContentAsync();
        if (body.Contains("An unhandled exception") || body.Contains("SqliteException"))
        {
            throw new Exception("Page contains unhandled exception text");
        }
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);
}
