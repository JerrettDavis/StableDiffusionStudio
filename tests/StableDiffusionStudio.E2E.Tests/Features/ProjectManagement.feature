Feature: Project Management
  As a user of Stable Diffusion Studio
  I want to create and manage projects
  So that I can organize my image generation work

  Scenario: View empty projects page
    Given I am on the home page
    When I navigate to the projects page
    Then I should see the empty state message "No projects yet"

  Scenario: Create a new project
    Given I am on the projects page
    When I click the "New Project" button
    And I enter "My First Project" as the project name
    And I enter "Testing project creation" as the description
    And I click the "Create" button in the dialog
    Then I should see "My First Project" in the projects list
    And I should see a success notification

  Scenario: Navigate to project detail
    Given a project named "Test Project" exists
    And I am on the projects page
    When I click on the project "Test Project"
    Then I should see the project detail page
    And the page title should contain "Test Project"

  Scenario: Delete a project
    Given a project named "Deletable Project" exists
    And I am on the project detail page for "Deletable Project"
    When I open the project menu
    And I click "Delete" in the menu
    And I confirm the deletion
    Then I should be redirected to the projects page
    And I should not see "Deletable Project" in the projects list
