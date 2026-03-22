Feature: Workflow System
  As a user of Stable Diffusion Studio
  I want to create and manage multi-step generation workflows
  So that I can chain generation steps together for complex image pipelines

  Scenario: Workflows page loads without errors
    Given I am on the home page
    When I navigate to the workflows page
    Then I should see the "Workflows" heading
    And the page should not have any error messages

  Scenario: Workflows page shows New Workflow and Import buttons
    Given I am on the home page
    When I navigate to the workflows page
    Then I should see a "New Workflow" button
    And I should see an "Import" button

  Scenario: Create a new workflow
    Given I am on the home page
    When I navigate to the workflows page
    And I click the "New Workflow" button
    And I enter "E2E Test Workflow" in the dialog input
    And I click the "OK" button in the dialog
    Then I should be on the workflow editor page
    And I should see "E2E Test Workflow" in the toolbar

  Scenario: Workflow editor has node palette
    Given I have created a workflow named "Palette Test"
    When I am on the workflow editor page
    Then I should see "Node Palette" in the sidebar
    And I should see "Generate" in the node palette
    And I should see "Img2Img" in the node palette
    And I should see "Output" in the node palette
    And I should see "Upscale" in the node palette
    And I should see "Conditional" in the node palette
    And I should see "Script" in the node palette

  Scenario: Add nodes to workflow canvas
    Given I have created a workflow named "Canvas Test"
    When I am on the workflow editor page
    And I click "Generate" in the node palette
    Then I should see the "Generate" node on the canvas
    When I click "Output" in the node palette
    Then I should see the "Output" node on the canvas

  Scenario: Select a node and see property panel
    Given I have created a workflow named "Property Test"
    When I am on the workflow editor page
    And I click "Generate" in the node palette
    And I click the "Generate" node on the canvas
    Then I should see the property panel
    And I should see "Generation Parameters" in the property panel

  Scenario: Save workflow
    Given I have created a workflow named "Save Test"
    When I am on the workflow editor page
    And I click the "Save" button
    Then I should see a success notification

  Scenario: Duplicate workflow
    Given I have created a workflow named "Original Workflow"
    When I am on the workflow editor page
    And I click the "Duplicate" button
    Then I should be on a different workflow editor page
    And I should see "Original Workflow (copy)" in the toolbar

  Scenario: Delete workflow from list
    Given I am on the home page
    When I navigate to the workflows page
    And I create a workflow named "Delete Me"
    Then I should see "Delete Me" in the workflow list
    When I delete the workflow "Delete Me"
    Then I should not see "Delete Me" in the workflow list

  Scenario: Workflows page shows starter templates after first load
    Given I am on the home page
    When I navigate to the workflows page
    And I wait for templates to load
    Then I should see "Basic Generation" in the workflow list
    And I should see "Generate + Upscale" in the workflow list
    And I should see "Generate + Refine" in the workflow list

  Scenario: Run History panel shows when no node is selected
    Given I have created a workflow named "History Test"
    When I am on the workflow editor page
    Then I should see "Run History" in the sidebar
    And I should see "No runs yet" in the sidebar
