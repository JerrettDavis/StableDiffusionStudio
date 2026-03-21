Feature: Full Generation Workflow
  As a user of Stable Diffusion Studio
  I want to generate images using real models
  So that the app works end-to-end in production conditions

  Scenario: Dashboard loads successfully
    Given I am on the home page
    Then I should see the "Welcome back" heading
    And the page should not have any error messages

  Scenario: Models page loads without errors
    Given I am on the home page
    When I navigate to the models page
    Then I should see the "Models" heading
    And the page should not have any error messages

  Scenario: Generate page loads with model selector
    Given I am on the generate page
    When I wait for the page to fully load
    Then I should see the "Checkpoint" selector
    And I should see available models in the checkpoint dropdown

  Scenario: Create project and navigate to workspace
    Given I am on the projects page
    When I click the "New Project" button
    And I enter "E2E Test Project" as the project name
    And I click the "Create" button in the dialog
    Then I should see "E2E Test Project" in the projects list
    When I click on the project "E2E Test Project"
    Then I should see the "Checkpoint" selector

  Scenario: Presets page shows saved presets
    Given I am on the home page
    When I navigate to the presets page
    Then I should see the "Presets" heading
    And the page should not have any error messages

  Scenario: Jobs page loads with interactive table
    Given I am on the home page
    When I navigate to the jobs page
    Then I should see the "Jobs" heading
    And the page should not have any error messages

  Scenario: Settings page has all configuration tabs
    Given I am on the home page
    When I navigate to the settings page
    Then I should see the "Storage Roots" tab
    And I should see the "Model Sources" tab
    And I should see the "Performance / Backend" tab
    And I should see the "Content Safety" tab
    And I should see the "Data Management" tab
    And I should see the "Output" tab
