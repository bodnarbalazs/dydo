@DYD-96
Feature: Gate adapters preserve measurement and cleanup outcomes
  The project gate adapter translates native tool outcomes to the shared gate contract.
  Tests retain their existing native-command exit behavior.

  Scenario Outline: A completed gate adapter preserves its contracted outcome
    Given a configured "<gate>" fixture adapter that completes with native exit <native>
    And the adapter produces its declared evidence and completes owned cleanup before returning
    When I invoke that gate through each canonical and derived testing facade
    Then its result row has state "<state>", childExit <native> and resultExit <result>
    And its aggregate result and process exit are both <result>
    And the published result retains schema 1 and the existing artifact shape

    Examples:
      | gate     | native | state       | result |
      | static   | 0      | passed      | 0      |
      | static   | 1      | failed      | 1      |
      | static   | 2      | invalid     | 2      |
      | static   | 17     | failed      | 1      |
      | static   | 130    | interrupted | 130    |
      | coverage | 0      | passed      | 0      |
      | coverage | 1      | failed      | 1      |
      | coverage | 2      | invalid     | 2      |
      | coverage | 17     | failed      | 1      |
      | coverage | 130    | interrupted | 130    |
      | mutation | 0      | passed      | 0      |
      | mutation | 1      | failed      | 1      |
      | mutation | 2      | invalid     | 2      |
      | mutation | 17     | failed      | 1      |
      | mutation | 130    | interrupted | 130    |

  Scenario Outline: An ordinary completed test command keeps nonzero failure normalization
    Given a configured test fixture command that normally returns native exit <native>
    And the facade itself receives no interruption
    When I invoke the test through each canonical and derived testing facade
    Then its result row has state "failed", childExit <native> and resultExit 1
    And its aggregate result and process exit are both 1

    Examples:
      | native |
      | 2      |
      | 130    |

  Scenario Outline: Missing measurement does not stop independent gate rows
    Given three ordered independent "<gate>" adapters returning native exits 1, 2 and 0
    When I invoke that gate through each canonical and derived testing facade
    Then all three adapters execute once in manifest order
    And their ordered row states are failed, invalid and passed with unchanged child exits
    And the aggregate result and process exit are both 2

    Examples:
      | gate     |
      | static   |
      | coverage |
      | mutation |

  Scenario Outline: A cleaned-up interrupted gate prevents later dispatch
    Given an ordered "<gate>" adapter that starts a long-lived owned child and then cancels it
    And the adapter waits for that child to stop and removes its owned scratch before returning 130
    And a later independent adapter would write a dispatch marker
    When I invoke that gate through each canonical and derived testing facade
    Then the interrupted row is published only after the owned child has stopped and scratch is absent
    And the later adapter does not execute and its dispatch marker is absent
    And the interrupted row retains childExit 130 and resultExit 130
    And the aggregate result and process exit are both 130

    Examples:
      | gate     |
      | static   |
      | coverage |
      | mutation |
