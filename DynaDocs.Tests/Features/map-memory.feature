@map-memory
Feature: Remember project maps
  Linear owns live work. A saved map is a disposable picture of the last successful fetch.

  Scenario: First visit fetches a fresh map normally
    Given an isolated map cache
    When I open project "p-map" with Linear title "First"
    Then the fresh map title is "First"
    And the saved map title is "First"

  Scenario: A restart on a different port remembers the project
    Given an isolated map cache
    When I open project "p-map" with Linear title "Before restart"
    And I restart the map server
    Then the saved map title is "Before restart"
    And reading the saved map made no Linear request

  Scenario: A failed fetch retains the saved map and a successful Refresh replaces it
    Given an isolated map cache
    When I open project "p-map" with Linear title "Saved"
    And Linear fails on Refresh
    Then the saved map title is "Saved"
    When I open project "p-map" with Linear title "Recovered"
    Then the saved map title is "Recovered"

  Scenario Outline: Projects and credentials have independent saved maps
    Given an isolated map cache
    When I open project "p-map" with Linear title "Private"
    And I use project "<project>" and key "<key>" and endpoint "<endpoint>"
    Then the saved map is missing
    Examples:
      | project | key      | endpoint                        |
      | other   | test-key | https://api.linear.app/graphql  |
      | p-map   | rotated  | https://api.linear.app/graphql  |
      | p-map   | test-key | https://another.test/graphql    |

  Scenario Outline: Unusable cache data falls back to a fresh fetch
    Given an isolated map cache
    When I open project "p-map" with Linear title "Old"
    And the saved map becomes "<damage>"
    Then the saved map is missing
    When I open project "p-map" with Linear title "Fresh"
    Then the fresh map title is "Fresh"
    Examples:
      | damage        |
      | corrupt       |
      | incompatible  |
      | unreadable    |
      | unwritable    |

  Scenario: Simultaneous writers leave a complete saved map
    Given an isolated map cache
    When simultaneous writers save different maps
    Then one complete saved map remains

  Scenario: Saved B stays available while A is fetching
    Given an isolated map cache
    When I open project "p-map" with Linear title "Saved B"
    And another project has a blocked Linear fetch
    Then the saved map title is "Saved B"
    And the blocked fetch ends when the server stops
