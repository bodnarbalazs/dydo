@DYD-501
Feature: Validation reports a Claude project that runs without the dydo guard
  A project that records the Claude integration commits its guard hook in .claude/settings.json.
  When those settings drift and lose the hook, validation names the fix, and rejoining restores it.

  Scenario: Rejoining Claude restores the guard that validation reported missing
    Given an empty project directory
    When I initialize dydo with "claude"
    Then the command succeeds
    Given the Claude settings no longer run the dydo guard
    When I validate the project
    Then validation passes with exactly this warning:
      | file                  | warning                                                                                                                      |
      | .claude/settings.json | Claude agents run without the dydo guard: it has no PreToolUse hook running 'dydo guard --host claude'. Run 'dydo init claude --join'. |
    When I rejoin dydo with "claude"
    Then the command succeeds
    When I validate the project
    Then validation passes with no issues
