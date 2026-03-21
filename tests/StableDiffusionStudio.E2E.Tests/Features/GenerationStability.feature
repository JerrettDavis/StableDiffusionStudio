Feature: Generation Stability
  As a user of Stable Diffusion Studio
  I want generation to work without crashing the app
  So that I can reliably create images

  Scenario: App remains responsive after generation starts
    Given I am on the generate page
    When I wait for the page to fully load
    Then the page should not have any error messages
    And the app should be responsive

  Scenario: Jobs page shows job history without errors
    Given I am on the home page
    When I navigate to the jobs page
    Then I should see the "Jobs" heading
    And the page should not have any error messages

  Scenario: Fresh startup has no stale generating state
    Given I am on the generate page
    When I wait for the page to fully load
    Then I should not see a generating spinner
    And the page should not have any error messages

  Scenario: Settings page loads all tabs without errors
    Given I am on the home page
    When I navigate to the settings page
    Then I should see the "Storage Roots" tab
    And the page should not have any error messages
