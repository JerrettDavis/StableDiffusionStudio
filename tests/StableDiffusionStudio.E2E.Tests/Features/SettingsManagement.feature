Feature: Settings Management
  As a user of Stable Diffusion Studio
  I want to configure application settings
  So that the app knows where to find my models

  Scenario: View settings page
    Given I am on the home page
    When I navigate to the settings page
    Then I should see the "Storage Roots" tab

  Scenario: Navigation works correctly
    Given I am on the home page
    Then I should see navigation links for "Home", "Projects", "Models", "Jobs", and "Settings"
    When I navigate to the projects page
    Then I should see the "Projects" heading
    When I navigate to the models page
    Then I should see the "Models" heading
    When I navigate to the jobs page
    Then I should see the "Jobs" heading
