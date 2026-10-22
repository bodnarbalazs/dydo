@machine-mode
Feature: Machine mode
  A machine mode flag is read by every agent on this machine; overnight is the one flag.
  An agent's mode set is blocked once with a reminder. On the rerun the user approves it where the
  host can ask, and otherwise runs it in his own terminal.

  Scenario: Show lists every flag with its value
    Given a fresh machine mode
    When I run dydo mode show
    Then the mode shows "overnight" as "off"

  Scenario: Set changes a flag
    Given a fresh machine mode
    When I run dydo mode set "overnight" "on"
    Then the mode set succeeds
    And the mode shows "overnight" as "on"

  Scenario Outline: Set refuses an unknown flag or value
    Given a fresh machine mode
    When I run dydo mode set "<flag>" "<value>"
    Then the mode set is refused naming "<refused>"
    And the mode shows "overnight" as "off"

    Examples:
      | flag      | value | refused  |
      | daylight  | on    | daylight |
      | overnight | maybe | maybe    |

  Scenario: An agent's first mode set is blocked with a reminder
    Given an agent on "Claude Code" in "default" permission mode
    When the agent runs "dydo mode set overnight on"
    Then the guard blocks it and tells the chief of staff to run it again

  Scenario Outline: The rerun asks the user on Claude Code in an asking mode
    Given an agent on "Claude Code" in "<permission mode>" permission mode
    When the agent runs "dydo mode set overnight on" twice
    Then the guard asks the user about "dydo mode set overnight on"

    Examples:
      | permission mode |
      | default         |
      | acceptEdits     |
      | plan            |
      | auto            |

  Scenario Outline: The rerun is denied where the host cannot ask
    Given an agent on "<host>" in "<permission mode>" permission mode
    When the agent runs "dydo mode set overnight on" twice
    Then the guard denies it and asks the user to run "dydo mode set overnight on" in their own terminal

    Examples:
      | host        | permission mode   |
      | Codex       | default           |
      | Claude Code | bypassPermissions |
      | Claude Code | dontAsk           |
      | unknown     | default           |
