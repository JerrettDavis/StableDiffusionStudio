Feature: Model Browsing
  As a user of Stable Diffusion Studio
  I want to browse and discover AI models
  So that I can select models for image generation

  Scenario: View empty models page
    Given I am on the home page
    When I navigate to the models page
    Then I should see the empty state message "No models discovered"

  Scenario: Add a storage directory
    Given I am on the models page
    When I click the "Add Directory" button
    And I enter a valid directory path
    And I enter "Test Models" as the display name
    And I click the "Add" button in the dialog
    Then I should see a notification about adding the directory

  Scenario: View the dashboard
    Given I am on the home page
    Then I should see the "Welcome back" heading
    And I should see the "Quick Actions" section
    And I should see a "Generate" button
