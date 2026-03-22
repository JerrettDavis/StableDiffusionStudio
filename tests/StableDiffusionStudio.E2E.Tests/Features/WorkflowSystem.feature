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

  Scenario: Create a new workflow and open editor
    Given I have created a workflow named "E2E Test Workflow"
    Then I should be on the workflow editor page
    And I should see "E2E Test Workflow" in the toolbar

  Scenario: Workflow editor shows node palette
    Given I have created a workflow named "Palette Test"
    When I am on the workflow editor page
    Then I should see "Node Palette" in the sidebar

  Scenario: Save workflow shows success
    Given I have created a workflow named "Save Test"
    When I am on the workflow editor page
    And I click the "Save" button
    Then I should see a success notification

  Scenario: Delete workflow from list
    Given I am on the home page
    When I navigate to the workflows page
    And I create a workflow named "Delete Me"
    Then I should see "Delete Me" in the workflow list
    When I delete the workflow "Delete Me"
    Then I should not see "Delete Me" in the workflow list

  Scenario: Run History panel shows when no node is selected
    Given I have created a workflow named "History Test"
    When I am on the workflow editor page
    Then I should see "Run History" in the sidebar
