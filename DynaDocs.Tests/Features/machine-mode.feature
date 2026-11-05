@machine-mode
Feature: Machine mode
  A machine mode flag is read by every agent on this machine; overnight is the one flag.
  An executing mode set warns the chief of staff once, then passes the same command's retry.
  Activation and shutdown require an independent readback after the actual command.

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

  Scenario Outline: Any agent may show the mode
    Given <caller> on "Claude Code" in "default" permission mode
    When the agent runs "dydo mode show"
    Then the guard allows it

    Examples:
      | caller      |
      | an agent    |
      | a sub-agent |

  Scenario: A sub-agent's mode set meets the same reminder and retry
    Given a sub-agent on "Claude Code" in "default" permission mode
    When the agent runs "dydo mode set overnight on" twice
    Then the guard allows it

  Scenario: An agent's first mode set is blocked with a reminder
    Given an agent on "Claude Code" in "default" permission mode
    When the agent runs "dydo mode set overnight on"
    Then the guard blocks it and tells the chief of staff to run it again

  Scenario Outline: Every host warns once and passes the same transition's retry
    Given an agent on "<host>" in "<permission mode>" permission mode
    When the agent runs "dydo mode set overnight <value>" twice
    Then the guard allows it

    Examples:
      | host                                    | permission mode   | value |
      | Claude Code                             | default           | on    |
      | Claude Code                             | default           | off   |
      | Claude Code                             | acceptEdits       | on    |
      | Claude Code                             | plan              | on    |
      | Claude Code                             | auto              | on    |
      | Claude Code                             | bypassPermissions | on    |
      | Claude Code                             | bypassPermissions | off   |
      | Claude Code                             | dontAsk           | on    |
      | Claude Code                             | dontAsk           | off   |
      | Codex                                   | default           | on    |
      | Codex                                   | default           | off   |
      | T3                                      | default           | on    |
      | T3                                      | default           | off   |
      | direct guard                            | default           | on    |
      | direct guard                            | default           | off   |
      | Codex launched from a Claude Code shell | default           | on    |
      | Codex launched from a Claude Code shell | default           | off   |
      | Claude Code without the init marker     | default           | on    |
      | Claude Code without the init marker     | default           | off   |
      | unknown                                 | default           | on    |
      | unknown                                 | default           | off   |

  Scenario: Opposite transitions and different full commands each receive their own warning
    Given an agent on "Codex" in "default" permission mode
    When the agent runs "dydo mode set overnight on"
    Then the guard blocks it and tells the chief of staff to run it again
    When the agent runs "dydo mode set overnight off"
    Then the guard blocks it and tells the chief of staff to run it again
    When the agent runs "dydo mode set overnight on; echo done"
    Then the guard blocks it and tells the chief of staff to run it again
    When the agent runs "dydo mode set overnight on"
    Then the guard allows it
    When the agent runs "dydo mode set overnight off"
    Then the guard allows it

  Scenario Outline: Warning and retry preserve owned state until the setter executes and is read back
    Given a fresh machine mode
    And the owned overnight mode starts as "<initial>"
    And an agent on "Codex" in "default" permission mode
    When the agent runs "dydo mode set overnight <target>"
    Then the guard blocks it and tells the chief of staff to run it again
    And the owned mode state is unchanged
    When the agent runs "dydo mode set overnight <target>"
    Then the guard allows it
    And the owned mode state is unchanged
    When I run dydo mode set "overnight" "<target>"
    Then the mode set succeeds
    And the mode shows "overnight" as "<target>"

    Examples:
      | initial | target |
      | off     | on     |
      | on      | off    |

  Scenario Outline: A quoted, pathed or indirect mode set meets the same reminder
    Given an agent on "Claude Code" in "default" permission mode
    When the agent runs the command: <command>
    Then the guard blocks it and tells the chief of staff to run it again

    Examples:
      | command                                                           |
      | dydo mode "set" overnight on                                      |
      | dydo 'mode' set overnight on                                      |
      | "dydo" mode set overnight on                                      |
      | d"y"do mode set overnight on                                      |
      | "C:/tools/dydo.exe" mode set overnight on                         |
      | & "dydo" mode set overnight on                                    |
      | dotnet bin/Release/net10.0/dydo.dll mode set overnight on         |
      | dotnet run -- mode set overnight on                               |
      | git status && dydo mode set overnight on                          |
      | git status; "dydo" mode set overnight on                          |
      | cmd /c "dydo mode set overnight on"                               |
      | Start-Process dydo -ArgumentList "mode", "set", "overnight", "on" |
      | env -i dydo mode set overnight on                                 |
      | & "C:/Program Files/dydo.exe" mode set overnight on               |
      | pwsh -NoProfile -Command "& dydo mode set overnight on"           |
      | bash -c 'cd /tmp && dydo mode set overnight on'                   |
      | DYDO.EXE mode set overnight on                                    |

  Scenario Outline: A command that only mentions mode set passes
    Given an agent on "Claude Code" in "default" permission mode
    When the agent runs the command: <command>
    Then the guard allows it

    Examples:
      | command                                                      |
      | git commit -m "docs: explain dydo mode set overnight on"     |
      | gh issue comment 12 --body "run dydo mode set overnight off" |
      | echo dydo mode set overnight on                              |
      | git commit -m "fix guard; dydo mode set must ask"            |
      | gh pr comment 12 --body "Tests pass; dydo mode set now asks" |
