Feature: Read checked TLA+ results as a statechart
  Scenario: A broken model shows its shortest bug path
    Given a bounded counter model with an unchanged safety invariant
    When the user runs statechart through PowerShell and bash
    Then TLC fails the invariant and the report shows its shortest path
    And Pele renders the chart with its bug marks

  Scenario: Correcting the model preserves the invariant
    Given the same invariant and a corrected transition
    When the user runs statechart
    Then TLC passes and the chart has no bug mark

  Scenario: The chart budget preserves counterexamples
    Given a bounded model with more than 500 states
    When the user runs statechart
    Then only shortest bug paths are drawn when a rule breaks
    And no chart is written when every rule holds

  Scenario: Both hosts require user invocation
    Given the canonical statechart skill
    When setup projects the skill into both discovery roots
    Then both projections resolve to the canonical skill
    And Claude and Codex disable implicit invocation
