@DYD-99
Feature: A fresh installation remains usable
  A project can adopt dydo without this repository's private working context.
  Checking, synchronization and framework updates keep the installation usable.

  Scenario Outline: Maintain a fresh installation for each integration selection
    Given an empty project directory
    When I initialize dydo with "<integration>"
    Then the command succeeds
    And the recorded Claude integration is <claude>
    And the recorded Codex integration is <codex>
    When I check the documentation
    Then the documentation has no validation errors and exactly these onboarding warnings:
      | document                   | warning                                                     |
      | understand/about.md        | About.md is not customized. Consider updating it.            |
      | understand/architecture.md | Architecture.md is not customized. Consider updating it.     |
    When I customize the foundation documents for a tiny task-list project
    And I check the documentation
    Then the documentation has no validation errors or warnings
    When I synchronize the native artifacts
    Then the command succeeds
    When I synchronize the native artifacts again
    Then the command succeeds
    And the native artifacts have identical paths and bytes
    And the customized foundation documents retain their paths and bytes
    When I check the documentation
    Then the documentation has no validation errors or warnings
    Given a user-owned file named "user-notes.txt" containing "My project notes."
    When I update the framework templates
    Then the command succeeds
    And the user-owned file named "user-notes.txt" still contains "My project notes."
    And the customized foundation documents retain their paths and bytes
    When I check the documentation
    Then the documentation has no validation errors or warnings

    Examples:
      | integration | claude | codex |
      | none        | false  | false |
      | claude      | true   | false |
      | codex       | false  | true  |
      | all         | true   | true  |
