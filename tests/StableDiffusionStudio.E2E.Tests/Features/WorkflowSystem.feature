Feature: Workflow System
  As a user of Stable Diffusion Studio
  I want to create and manage multi-step generation workflows
  So that I can chain generation steps together for complex image pipelines

  Scenario: Workflows page loads without errors
    Given I am on the home page
    When I navigate to the workflows page
    Then I should see the "Workflows" heading
    And the page should not have any error messages

  Scenario: Workflows page shows New Workflow button
    Given I am on the home page
    When I navigate to the workflows page
    Then I should see a "New Workflow" button

  Scenario: Create a workflow
    Given I am on the home page
    When I navigate to the workflows page
    And I create a workflow named "E2E Test Workflow"
    Then I should see "E2E Test Workflow" in the workflow list
