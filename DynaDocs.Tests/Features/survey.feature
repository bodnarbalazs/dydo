@survey
Feature: Survey a Project's lanes
  dydo survey reads one Linear Project on demand, with the user's key, and prints a short digest:
  one line per finding, sorted by kind and then key. It writes nothing and keeps nothing.

  Scenario: Each kind of finding is one line, sorted by kind and then key
    Given the Project's body is:
      """
      ## Goal
      - DYD-10 named outside the ledger
      ## Lane ledger — 2026-11-04 16:20 UTC
      | Lane | Tickets | State |
      | -- | -- | -- |
      | Core | [DYD-1](https://linear.app/x/issue/DYD-1/core) → DYD-2 | running |
      | Old | DYD-3 | waiting on review |
      | Shipped | DYD-4 | merged |
      - DYD-6 started lane
      - DYD-7 and DYD-11 question lane
      ## Notes
      - DYD-5 named after the ledger
      """
    And the Project has these issues:
      | key    | state       | assignee | parent | blocked by | label    | priority | project |
      | DYD-1  | In Progress | Ada      |        |            |          | 0        | Map     |
      | DYD-2  | Todo        |          |        | DYD-1      |          | 0        | Map     |
      | DYD-3  | Done        |          |        |            |          | 0        | Map     |
      | DYD-4  | Done        |          |        |            |          | 0        | Map     |
      | DYD-5  | Todo        |          |        | DYD-4      |          | 0        | Map     |
      | DYD-6  | In Progress |          |        |            |          | 0        | Map     |
      | DYD-7  | Backlog     | Ada      |        |            | Question | 0        | Map     |
      | DYD-8  | Todo        |          | DYD-1  |            |          | 0        | Map     |
      | DYD-9  | Backlog     |          |        | DYD-3      |          | 0        | Other   |
      | DYD-10 | Todo        | Ada      |        |            |          | 0        | Map     |
      | DYD-11 | Backlog     | Ada      |        |            | Question | 2        | Map     |
    When I survey "25acbbb9a90b"
    Then the survey exits 0
    And the survey prints:
      """
      dydo survey Map <time>: 8 findings
      pickable DYD-8 Issue 8 — unstarted, unassigned, no open blocker
      question-no-priority DYD-7 Issue 7 — an open Question with no priority
      stale-row DYD-3 Issue 3 — the row names only closed issues: DYD-3 Done
      started-unassigned DYD-6 Issue 6 — In Progress, with no assignee
      unblocked DYD-5 Issue 5 — every blocker is closed: DYD-4
      unblocked DYD-9 Issue 9 — every blocker is closed: DYD-3
      unlaned DYD-5 Issue 5 — open, and no ledger row names it or its parent
      unlaned DYD-10 Issue 10 — open, and no ledger row names it or its parent
      """

  Scenario: Without a ledger, every open issue is unlaned
    Given the Project's body is:
      """
      ## Goal
      - DYD-1 ships the release
      """
    And the Project has these issues:
      | key   | state       | assignee | parent | blocked by | label | priority | project |
      | DYD-1 | In Progress | Ada      |        |            |       | 0        | Map     |
      | DYD-2 | Done        |          |        |            |       | 0        | Map     |
    When I survey "25acbbb9a90b"
    Then the survey exits 0
    And the survey prints:
      """
      dydo survey Map <time>: 2 findings
      no-ledger - Map — the Project body has no ## heading containing "ledger"
      unlaned DYD-1 Issue 1 — open, and the Project has no ledger
      """

  Scenario: A quiet Project prints only its header
    Given a quiet Project
    When I survey "25acbbb9a90b"
    Then the survey exits 0
    And the survey prints:
      """
      dydo survey Map <time>: 0 findings
      """

  Scenario Outline: The argument is a Project UUID, slugId or Project URL
    Given a quiet Project
    When I survey "<argument>"
    Then the survey exits <exit>
    And Linear was asked for Project "<asked>"
    Examples:
      | argument                                                   | exit | asked                                |
      | 4e6aa9da-f325-4b53-a318-deba4b1b4d26                       | 0    | 4e6aa9da-f325-4b53-a318-deba4b1b4d26 |
      | 25acbbb9a90b                                               | 0    | 25acbbb9a90b                         |
      | https://linear.app/x/project/map-25acbbb9a90b/overview     | 0    | 25acbbb9a90b                         |
      | P-DYD-46                                                   | 2    | nothing                              |

  Scenario: An unknown Project is a usage error
    Given Linear has no such Project
    When I survey "25acbbb9a90b"
    Then the survey exits 2
    And the error says "no such Linear Project: 25acbbb9a90b"

  Scenario: Without LINEAR_API_KEY the survey prints dydo map's key help
    Given a quiet Project
    And LINEAR_API_KEY is not set
    When I survey "25acbbb9a90b"
    Then the survey exits 2
    And the error is dydo map's missing-key help
    And Linear was asked for Project "nothing"

  Scenario Outline: A Linear failure exits with the tool-error code and names the failure
    Given Linear fails with "<failure>"
    When I survey "25acbbb9a90b"
    Then the survey exits 2
    And the error says "<message>"
    Examples:
      | failure        | message                        |
      | HTTP 500       | Linear answered HTTP 500       |
      | a rejected key | Linear rejected LINEAR_API_KEY |
      | no answer      | Could not reach Linear         |

  Scenario: The survey only reads
    Given a quiet Project
    When I survey "25acbbb9a90b"
    Then every Linear request was a query
