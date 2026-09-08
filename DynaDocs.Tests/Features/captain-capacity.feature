@DYD-141
Feature: CaptainCapacity
  The captain keeps crew dispatch and exact scope when native thread capacity is constrained.

  Scenario: An Admiral commissions work only through the Issue Captain
    Given a pickable Issue has a captain, an exact worker brief, and a native task-tree budget
    When the next captain-owned stage is runnable
    Then the Admiral commissions or resumes the Issue Captain from the record
    And only that captain commissions the stage with its exact scope
    And the worker return goes to the captain
    And the canonical Admiral and Issue Captain prompts and the shared guide state that a saved brief is a portable handoff and never Admiral-to-crew authority

  Scenario: A captain budgets a complete serial delivery chain
    Given the Issue requires specification, production, hardening, fresh Issue review, and a later Merge Sub-issue
    When native capacity cannot hold those agents concurrently
    Then the captain schedules the stages serially within the configured open-thread budget
    And completion or interruption is not treated as slot release without native evidence
    And no required fresh specifier or reviewer is reused or omitted to fit the budget

  Scenario: A capacity refusal preserves hierarchy and scope
    Given the native inventory accounts for the configured open spawned-thread budget
    When the captain makes one bounded attempt to commission the exact next stage and the host refuses it for capacity
    Then the captain preserves the record, candidate, hop SHA, and exact brief
    And the captain does not broaden the brief, retry blindly, or ask the Admiral to dispatch the crew
    And the Admiral does not dispatch that saved brief directly
    And documented lifecycle handling is used only when its effect is established for this host
    And if no documented handling makes the captain-owned stage runnable, the captain releases or returns the concrete limitation for escalation through Admiral to human

  Scenario: Configuration and observed capability remain separate evidence
    Given the local configuration records agents.max_concurrent_threads_per_session as 16 with preservation evidence
    And a completed researcher disappeared before or with one accepted replacement spawn
    When later necessary captain and fresh spec-review spawns are refused at four live inventory entries
    Then the evidence records that configuration consumption and effectiveness remain unproved in the existing task
    And it treats the accepted replacement as possible ordinary reclamation
    And it does not claim desktop reload, slot reclamation, backend, version, model, or lifecycle behavior
    And it leaves durable project configuration emission to DYD-86 and broader lifecycle claims to DYD-88
