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
    And the Git working tree is clean

  Scenario: Keep both hosts' metadata and resources beside one body
    Then the canonical "teach" skill keeps its Claude invocation frontmatter
    And the canonical "teach" skill keeps its Codex invocation metadata
    And every resource linked by the canonical "teach" body resolves inside its skill directory

  Scenario: Resolve skill links from canonical and projected locations
    Then every skill link resolves when followed from each location
      | location                       |
      | skills/<name>                  |
      | .claude/skills/<name>          |
      | .agents/skills/<name>          |

  Scenario: Remove stale two-source guidance
    Then current documentation and template mirrors describe skills/<name> as the only editable source
    And canonical agent guidance does not instruct agents to maintain or compare per-host skill copies

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
