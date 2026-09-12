@DYD-91
Feature: One canonical skill tree reaches every supported host
  Each role is authored once under skills and exposed through host-native discovery roots.
  Setup preserves human-owned paths and does not recreate a compiler or a third OpenCode copy.

  Background:
    Given a project containing the canonical "teach" skill and the skill setup script

  Scenario: Project whole skill directories into Claude and Codex
    Given unrelated Claude and Codex skills and host configuration files with recorded bytes
    When I set up the canonical skills
    Then setup succeeds
    And the Claude and Codex "teach" entries resolve to the canonical skill directory
    And no OpenCode "teach" projection is created
    And every unrelated skill and host configuration file keeps its recorded bytes
    When I set up the canonical skills again
    Then setup succeeds
    And the host projections are unchanged
    And every unrelated skill and host configuration file keeps its recorded bytes

  Scenario: Keep both hosts' metadata and resources beside one body
    Then the canonical "teach" skill keeps its Claude invocation frontmatter
    And the canonical "teach" skill keeps its Codex invocation metadata
    And every resource linked by the canonical "teach" body resolves inside its skill directory

  Scenario: Resolve project knowledge from the canonical directory
    Then every project-knowledge link in the canonical skill tree resolves from skills/<name>

  Scenario: Refuse a human-owned host directory before changing another host
    Given a human-owned Claude "teach" directory
    When I set up the canonical skills
    Then setup fails without changing the human-owned directory
    And no Codex "teach" projection is created

  Scenario: Preflight every deterministic collision before creating a projection
    Given no Claude "teach" entry exists
    And a human-owned Codex "teach" directory exists
    When I set up the canonical skills
    Then setup fails without changing the human-owned directory
    And no Claude "teach" projection is created

  Scenario: Refuse a host link that targets another skill tree
    Given the Claude "teach" entry links to a different directory
    When I set up the canonical skills
    Then setup fails without replacing the existing link
    And no Codex "teach" projection is created
