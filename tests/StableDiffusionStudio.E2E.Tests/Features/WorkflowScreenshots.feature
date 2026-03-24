Feature: Workflow Page Screenshots
  As a developer
  I want to capture screenshots of the workflow pages
  So that I can visually verify the UX before and after changes

  @screenshot
  Scenario: Capture workflows list page at 1080p (empty state)
    Given I am on the home page
    And the viewport is "1080p"
    When I navigate to the workflows page
    Then I take a screenshot named "workflows-list-empty-1080p"

  @screenshot
  Scenario: Capture workflows list page at 4K (empty state)
    Given I am on the home page
    And the viewport is "4K"
    When I navigate to the workflows page
    Then I take a screenshot named "workflows-list-empty-4k"

  @screenshot
  Scenario: Capture workflows list page with templates at 1080p
    Given I am on the home page
    When I navigate to the workflows page
    And I wait for templates to load
    And the viewport is "1080p"
    Then I take a screenshot named "workflows-list-templates-1080p"

  @screenshot
  Scenario: Capture workflows list page with templates at 4K
    Given I am on the home page
    When I navigate to the workflows page
    And I wait for templates to load
    And the viewport is "4K"
    Then I take a screenshot named "workflows-list-templates-4k"

  @screenshot
  Scenario: Capture workflow editor page at 1080p
    Given I have created a workflow named "Screenshot Test"
    And the viewport is "1080p"
    Then I take a screenshot named "workflow-editor-1080p"

  @screenshot
  Scenario: Capture workflow editor page at 4K
    Given I have created a workflow named "Screenshot Test 4K"
    And the viewport is "4K"
    Then I take a screenshot named "workflow-editor-4k"
