@DYD-110
Feature: An experimental beta can be dogfooded honestly
  The installed prerelease carries the reviewed template snapshot that was built into it.
  Its generated roles expose the capabilities needed for the first local finishing pass.

  Scenario: Ignore a consumer's source-looking template decoy
    Given an empty project directory
    When I initialize dydo with "all"
    Then the command succeeds
    Given the project looks like a dydo source checkout with a conflicting skill template
    And custom native host files and hook entries have been recorded
    When I synchronize the native artifacts
    Then the command succeeds
    And the conflicting source template is not discovered or emitted
    And every emitted native skill matches the beta's embedded template inventory
    And custom native host files outside managed hooks retain their paths and bytes
    And custom Codex hook entries remain semantically intact
    When I synchronize the native artifacts again
    Then the command succeeds
    And the native artifacts have identical paths and bytes
    And custom native host files outside managed hooks retain their paths and bytes
    And custom Codex hook entries remain semantically intact

  Scenario Outline: Emit Codex capabilities from authored role metadata
    Given an empty project directory
    When I initialize dydo with "all"
    Then the command succeeds
    When I synchronize the native artifacts
    Then the command succeeds
    And the Codex agent "<role>" has delegation <delegation> and web access <web>

    Examples:
      | role          | delegation | web   |
      | issue-captain | true       | false |
      | research      | true       | true  |
      | scout         | false      | true  |
      | reviewer      | false      | false |
