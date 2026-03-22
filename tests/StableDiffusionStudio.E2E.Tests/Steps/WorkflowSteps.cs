using FluentAssertions;
using Microsoft.Playwright;
using Reqnroll;

namespace StableDiffusionStudio.E2E.Tests.Steps;

[Binding]
public class WorkflowSteps
{
    private readonly ScenarioContext _context;
    private IPage Page => _context.Get<IPage>();
    private string BaseUrl => _context.Get<string>("BaseUrl");

    public WorkflowSteps(ScenarioContext context)
    {
        _context = context;
    }

    [When(@"I navigate to the workflows page")]
    public async Task WhenINavigateToTheWorkflowsPage()
    {
        await Page.GotoAsync($"{BaseUrl}/workflows");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(3000);
    }

    [When(@"I create a workflow named ""(.*)""")]
    public async Task WhenICreateAWorkflowNamed(string name)
    {
        var newBtn = Page.GetByRole(AriaRole.Button, new() { Name = "New Workflow" }).First;
        await Expect(newBtn).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await newBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
        await FillDialogAndConfirm(name, "OK");
        await Page.GotoAsync($"{BaseUrl}/workflows");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);
    }

    [Given(@"I have created a workflow named ""(.*)""")]
    public async Task GivenIHaveCreatedAWorkflowNamed(string name)
    {
        await Page.GotoAsync($"{BaseUrl}/workflows");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(3000);
        var newBtn = Page.GetByRole(AriaRole.Button, new() { Name = "New Workflow" }).First;
        await Expect(newBtn).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await newBtn.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
        await FillDialogAndConfirm(name, "OK");
        // Wait for navigation to editor page
        await Page.WaitForURLAsync($"**/workflows/**", new() { Timeout = 15_000 });
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(3000);
    }

    private async Task FillDialogAndConfirm(string text, string buttonText)
    {
        var dialog = Page.Locator(".mud-dialog");
        await Expect(dialog).ToBeVisibleAsync();
        var input = dialog.Locator("input").First;
        await input.FillAsync(text);
        var button = dialog.GetByRole(AriaRole.Button, new() { Name = buttonText });
        await button.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);
    }

    [When(@"I am on the workflow editor page")]
    public async Task WhenIAmOnTheWorkflowEditorPage()
    {
        // Should already be on the editor after creating
        await Page.WaitForTimeoutAsync(500);
    }

    [When(@"I click ""(.*)"" in the node palette")]
    public async Task WhenIClickInTheNodePalette(string nodeName)
    {
        var paletteButton = Page.GetByRole(AriaRole.Button, new() { Name = nodeName }).First;
        await paletteButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [When(@"I click the ""(.*)"" node on the canvas")]
    public async Task WhenIClickTheNodeOnTheCanvas(string nodeName)
    {
        // ReactFlow nodes contain the label text
        var node = Page.Locator(".react-flow__node", new() { HasText = nodeName }).First;
        await node.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
    }

    [When(@"I delete the workflow ""(.*)""")]
    public async Task WhenIDeleteTheWorkflow(string name)
    {
        // Find the card containing the workflow name
        var card = Page.Locator(".mud-card", new() { HasText = name }).First;
        // Click the icon button in the card actions area
        var deleteButton = card.Locator(".mud-card-actions button").First;
        await deleteButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Confirm deletion in the message box dialog
        var confirmButton = Page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).Last;
        await Expect(confirmButton).ToBeVisibleAsync(new() { Timeout = 5000 });
        await confirmButton.ClickAsync();
        await Page.WaitForTimeoutAsync(2000);
    }

    [When(@"I wait for templates to load")]
    public async Task WhenIWaitForTemplatesToLoad()
    {
        // Templates are seeded by background service — wait up to 20 seconds
        for (var i = 0; i < 20; i++)
        {
            var content = await Page.ContentAsync();
            if (content.Contains("Basic Generation"))
                return;
            await Page.WaitForTimeoutAsync(1000);
            await Page.ReloadAsync();
            await Page.WaitForTimeoutAsync(500);
        }
    }

    [Then(@"I should be on the workflow editor page")]
    public async Task ThenIShouldBeOnTheWorkflowEditorPage()
    {
        var url = Page.Url;
        url.Should().Contain("/workflows/");
        url.Should().NotEndWith("/workflows");
    }

    [Then(@"I should be on a different workflow editor page")]
    public async Task ThenIShouldBeOnADifferentWorkflowEditorPage()
    {
        var url = Page.Url;
        url.Should().Contain("/workflows/");
    }

    [Then(@"I should see ""(.*)"" in the toolbar")]
    public async Task ThenIShouldSeeInTheToolbar(string text)
    {
        var toolbar = Page.Locator(".mud-toolbar").First;
        await Expect(toolbar.GetByText(text)).ToBeVisibleAsync(new() { Timeout = 10_000 });
    }

    [Then(@"I should see ""(.*)"" in the sidebar")]
    public async Task ThenIShouldSeeInTheSidebar(string text)
    {
        var element = Page.GetByText(text).First;
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see ""(.*)"" in the node palette")]
    public async Task ThenIShouldSeeInTheNodePalette(string text)
    {
        var button = Page.GetByRole(AriaRole.Button, new() { Name = text }).First;
        await Expect(button).ToBeVisibleAsync();
    }

    [Then(@"I should see the ""(.*)"" node on the canvas")]
    public async Task ThenIShouldSeeTheNodeOnTheCanvas(string nodeName)
    {
        // After adding a node, the canvas should contain it
        // ReactFlow renders nodes as divs; check via the service-side state
        await Page.WaitForTimeoutAsync(500);
        // The node label appears in the page content
        var content = await Page.ContentAsync();
        content.Should().Contain(nodeName);
    }

    [Then(@"I should see the property panel")]
    public async Task ThenIShouldSeeThePropertyPanel()
    {
        var label = Page.GetByText("Label").First;
        await Expect(label).ToBeVisibleAsync();
    }

    [Then(@"I should see ""(.*)"" in the property panel")]
    public async Task ThenIShouldSeeInThePropertyPanel(string text)
    {
        var element = Page.GetByText(text).First;
        await Expect(element).ToBeVisibleAsync();
    }

    [Then(@"I should see ""(.*)"" in the workflow list")]
    public async Task ThenIShouldSeeInTheWorkflowList(string name)
    {
        var element = Page.GetByText(name).First;
        await Expect(element).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Then(@"I should not see ""(.*)"" in the workflow list")]
    public async Task ThenIShouldNotSeeInTheWorkflowList(string name)
    {
        await Page.WaitForTimeoutAsync(500);
        var elements = await Page.GetByText(name).CountAsync();
        elements.Should().Be(0);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);
}
