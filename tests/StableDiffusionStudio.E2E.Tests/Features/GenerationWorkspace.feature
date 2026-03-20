Feature: Generation Workspace
  As a user of Stable Diffusion Studio
  I want to generate images using AI models
  So that I can create art and iterate on creative ideas

  Scenario: View generation workspace in project
    Given a project named "Gen Test" exists
    And I am on the project detail page for "Gen Test"
    Then I should see the "Checkpoint" selector
    And I should see the prompt input fields
    And I should see a "Generate" button

  Scenario: View standalone generate page
    Given I am on the home page
    When I navigate to the generate page
    Then I should see the "Checkpoint" selector
    And I should see the prompt input fields

  Scenario: Navigate to all main pages without errors
    Given I am on the home page
    Then I should see the "Dashboard" heading
    When I navigate to the projects page
    Then I should see the "Projects" heading
    When I navigate to the models page
    Then I should see the "Models" heading
    When I navigate to the jobs page
    Then I should see the "Jobs" heading
    When I navigate to the settings page
    Then I should see the "Settings" heading
    When I navigate to the generate page
    Then the page should not have any error messages
