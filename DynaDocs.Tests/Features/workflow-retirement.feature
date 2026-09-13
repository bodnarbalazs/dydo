@DYD-92
Feature: Synchronization retires dydo workflow output
  Native agents and skills remain available after workflow compilation is retired.
  Migration removes only the known retired outputs and preserves project-owned files.

  Scenario Outline: Synchronize a fresh project without workflow output
    Given an empty project directory
    When I initialize dydo with "<integration>"
    Then the command succeeds
    When I synchronize the native artifacts
    Then synchronization reports only the native artifacts for "<integration>"
    And the selected native agents and skills for "<integration>" are nonempty
    And the retired workflow directory is absent
    When I synchronize the native artifacts again
    Then synchronization reports only the native artifacts for "<integration>"
    And the retired workflow directory is absent
    And the native artifacts have identical paths and bytes

    Examples:
      | integration |
      | none        |
      | claude      |
      | codex       |
      | all         |

  Scenario Outline: Remove known retired workflows while preserving project-owned files
    Given an empty project directory
    When I initialize dydo with "<integration>"
    Then the command succeeds
    Given both retired workflow files and the project-owned workflow byte fixtures
    When I synchronize the native artifacts
    Then both retired workflow files are absent
    And the project-owned workflow files retain their exact paths and bytes
    And synchronization reports only the native artifacts for "<integration>"
    And the selected native agents and skills for "<integration>" are nonempty
    When I synchronize the native artifacts again
    Then both retired workflow files are absent
    And the project-owned workflow files retain their exact paths and bytes
    And synchronization reports only the native artifacts for "<integration>"
    And the native artifacts have identical paths and bytes

    Examples:
      | integration |
      | claude      |
      | codex       |

  Scenario Outline: Keep an unused retired workflow directory absent
    Given an empty project directory
    When I initialize dydo with "all"
    Then the command succeeds
    Given the retired workflow directory starts "<state>"
    When I synchronize the native artifacts
    Then the retired workflow directory is absent
    And synchronization reports only the native artifacts for "all"
    And the selected native agents and skills for "all" are nonempty
    When I synchronize the native artifacts again
    Then the retired workflow directory is absent
    And synchronization reports only the native artifacts for "all"
    And the native artifacts have identical paths and bytes

    Examples:
      | state        |
      | absent       |
      | empty        |
      | retired-only |
