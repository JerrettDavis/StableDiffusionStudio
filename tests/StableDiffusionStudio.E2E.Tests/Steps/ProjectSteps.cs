using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Steps;

[Binding]
public class ProjectSteps
{
    private readonly ScenarioContext _context;
    private IPage Page => _context.Get<IPage>();
    private string BaseUrl => _context.Get<string>("BaseUrl");

    public ProjectSteps(ScenarioContext context)
    {
        _context = context;
    }

    [Given(@"I am on the projects page")]
    public async Task GivenIAmOnTheProjectsPage()
    {
        await Page.GotoAsync($"{BaseUrl}/projects");
        await WaitForBlazorAsync();
    }

    [Given(@"a project named ""(.*)"" exists")]
    public async Task GivenAProjectNamedExists(string projectName)
    {
        // Create project via UI
        await Page.GotoAsync($"{BaseUrl}/projects");
        await WaitForBlazorAsync();

        // Click toolbar "New Project" button
        var toolbarButton = Page.Locator(".mud-toolbar").GetByRole(AriaRole.Button, new() { Name = "New Project" });
        var buttonCount = await toolbarButton.CountAsync();
        if (buttonCount > 0)
            await toolbarButton.ClickAsync();
        else
            await Page.GetByRole(AriaRole.Button, new() { Name = "New Project" }).First.ClickAsync();

        // Wait for dialog and fill in project name
        var dialog = Page.Locator(".mud-dialog");
        await dialog.WaitForAsync(new() { Timeout = 10_000 });

        // Use click + type to ensure MudBlazor bindings fire properly
        var input = dialog.Locator("input").First;
        await input.ClickAsync();
        await input.FillAsync(projectName);
        // Dispatch change event to trigger MudBlazor binding
        await input.DispatchEventAsync("change", new { });
        await Page.WaitForTimeoutAsync(300);

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000); // allow state to settle
    }

    [Given(@"I am on the project detail page for ""(.*)""")]
    public async Task GivenIAmOnTheProjectDetailPageFor(string projectName)
    {
        // Navigate to projects page and click on the project card
        await Page.GotoAsync($"{BaseUrl}/projects");
        await WaitForBlazorAsync();

        await Page.Locator(".mud-card").GetByText(projectName).First.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
        // Wait for breadcrumbs to confirm we're on the detail page
        await Page.Locator(".mud-breadcrumbs").WaitForAsync(new() { Timeout = 10_000 });
    }

    [When(@"I click the ""(.*)"" button")]
    public async Task WhenIClickTheButton(string buttonText)
    {
        await WaitForBlazorAsync();

        // Prefer toolbar button to avoid duplicates with empty state action buttons
        var toolbarButton = Page.Locator(".mud-toolbar").GetByRole(AriaRole.Button, new() { Name = buttonText });
        var buttonCount = await toolbarButton.CountAsync();

        if (buttonCount > 0)
            await toolbarButton.ClickAsync();
        else
            await Page.GetByRole(AriaRole.Button, new() { Name = buttonText }).First.ClickAsync();

        await Page.WaitForTimeoutAsync(1000); // allow dialog animation
    }

    [When(@"I click the ""(.*)"" button in the dialog")]
    public async Task WhenIClickTheButtonInTheDialog(string buttonText)
    {
        var dialog = Page.Locator(".mud-dialog");
        await dialog.GetByRole(AriaRole.Button, new() { Name = buttonText }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [When(@"I enter ""(.*)"" as the project name")]
    public async Task WhenIEnterAsTheProjectName(string name)
    {
        var dialog = Page.Locator(".mud-dialog");
        await dialog.WaitForAsync(new() { Timeout = 10_000 });
        var input = dialog.Locator("input").First;
        await input.ClickAsync();
        await input.FillAsync(name);
        await input.DispatchEventAsync("change", new { });
        await Page.WaitForTimeoutAsync(300);
    }

    [When(@"I enter ""(.*)"" as the description")]
    public async Task WhenIEnterAsTheDescription(string description)
    {
        // Description uses a textarea (MudTextField with Lines="3")
        var dialog = Page.Locator(".mud-dialog");
        var textarea = dialog.Locator("textarea").First;
        await textarea.ClickAsync();
        await textarea.FillAsync(description);
        await textarea.DispatchEventAsync("change", new { });
        await Page.WaitForTimeoutAsync(300);
    }

    [When(@"I click on the project ""(.*)""")]
    public async Task WhenIClickOnTheProject(string projectName)
    {
        // Click the project card, not breadcrumb or heading
        // ProjectCard renders the project name in a MudText inside a MudPaper
        await Page.Locator(".mud-card").GetByText(projectName).First.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
    }

    [When(@"I open the project menu")]
    public async Task WhenIOpenTheProjectMenu()
    {
        // The MudMenu with MoreVert icon
        await Page.Locator(".mud-menu button").ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [When(@"I click ""(.*)"" in the menu")]
    public async Task WhenIClickInTheMenu(string text)
    {
        await Page.Locator(".mud-popover-open").GetByText(text, new() { Exact = true }).ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [When(@"I confirm the deletion")]
    public async Task WhenIConfirmTheDeletion()
    {
        // The confirmation MessageBox dialog has a "Delete" button
        var dialog = Page.Locator(".mud-message-box");
        await dialog.WaitForAsync(new() { Timeout = 5_000 });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [Then(@"I should see the empty state message ""(.*)""")]
    public async Task ThenIShouldSeeTheEmptyStateMessage(string message)
    {
        var element = Page.GetByText(message);
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see ""(.*)"" in the projects list")]
    public async Task ThenIShouldSeeInTheProjectsList(string projectName)
    {
        var element = Page.GetByText(projectName);
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should not see ""(.*)"" in the projects list")]
    public async Task ThenIShouldNotSeeInTheProjectsList(string projectName)
    {
        // Wait for page to settle after navigation
        await Page.WaitForTimeoutAsync(1000);
        // Check that no card contains the project name
        var card = Page.Locator(".mud-card").GetByText(projectName);
        await Expect(card).Not.ToBeVisibleAsync();
    }

    [Then(@"I should see a success notification")]
    public async Task ThenIShouldSeeASuccessNotification()
    {
        var snackbar = Page.Locator(".mud-snackbar-content-message");
        await Expect(snackbar).ToBeVisibleAsync();
    }

    [Then(@"I should see the project detail page")]
    public async Task ThenIShouldSeeTheProjectDetailPage()
    {
        var breadcrumbs = Page.Locator(".mud-breadcrumbs");
        await Expect(breadcrumbs).ToBeVisibleAsync();
    }

    [Then(@"the page title should contain ""(.*)""")]
    public async Task ThenThePageTitleShouldContain(string text)
    {
        // The project detail page shows the name as h4 heading
        var heading = Page.Locator(".mud-typography-h4").GetByText(text);
        await Expect(heading).ToBeVisibleAsync();
    }

    [Then(@"I should be redirected to the projects page")]
    public async Task ThenIShouldBeRedirectedToTheProjectsPage()
    {
        await Page.WaitForURLAsync("**/projects");
    }

    /// <summary>
    /// Waits for Blazor Server circuit to be fully connected and interactive.
    /// </summary>
    private async Task WaitForBlazorAsync()
    {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForFunctionAsync(
            "() => window.Blazor && window.Blazor._internal !== undefined",
            null,
            new() { Timeout = 15_000 });
        // Brief wait for circuit to fully activate
        await Page.WaitForTimeoutAsync(500);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);
}
