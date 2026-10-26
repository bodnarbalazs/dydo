@machine-mode
Feature: Machine mode
  A machine mode flag is read by every agent on this machine; overnight is the one flag.
  Where the host can ask, an agent's mode set is blocked once with a reminder and the user approves
  the rerun. Where it cannot, every try is denied and the user runs it in his own terminal.

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

  Scenario: A sub-agent's mode set meets the same reminder and ask
    Given a sub-agent on "Claude Code" in "default" permission mode
    When the agent runs "dydo mode set overnight on" twice
    Then the guard asks the user about "dydo mode set overnight on"

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

  Scenario Outline: Every try is denied where the host cannot ask
    Given an agent on "<host>" in "<permission mode>" permission mode
    When the agent runs "dydo mode set overnight on" three times
    Then the guard denies every try and asks the user to run "dydo mode set overnight on" in their own terminal

    Examples:
      | host                                    | permission mode   |
      | Codex                                   | default           |
      | Codex launched from a Claude Code shell | default           |
      | Claude Code without the init marker     | default           |
      | Claude Code                             | bypassPermissions |
      | Claude Code                             | dontAsk           |
      | unknown                                 | default           |

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
