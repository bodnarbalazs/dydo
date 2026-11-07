@DYD-624 @DYD-618
Feature: Shared independent review PASS grammar
  PR readiness and merge read the same review fields.

  Scenario Outline: Canonical and colon fields remain usable in both commands
    Given an independent review line with candidate separator "<candidate>" and base separator "<base>"
    When I check that review with pr-ready and a pinned merge dry run
    Then both commands accept the review

    Examples:
      | candidate | base |
      | space     | space |
      | colon     | space |
      | space     | colon |
      | colon     | colon |

  Scenario Outline: An optional base does not prevent readiness or merge
    Given an independent review line without a parsed base "<suffix>"
    When I check that review with pr-ready and a pinned merge dry run
    Then both commands accept the review

    Examples:
      | suffix |
      |        |
      | ; base nope |

  Scenario: Prose-only PASS is refused before merge
    Given the independent review line "PASS - rubric/type: code, fix-hop re-review."
    When I check that review with pr-ready and a pinned merge dry run
    Then both commands refuse the review
