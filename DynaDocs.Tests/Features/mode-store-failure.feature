@mode-store-failure
Feature: Mode store failure
  When the mode store cannot be written, a mode set fails with one error that names the mode
  store path and the filesystem cause, prints nothing else, and leaves the stored state as it was.

  Scenario Outline: A mode set the mode store refuses fails with one error and keeps the state
    Given the owned mode store <store>
    When I set the owned overnight mode to "<target>"
    Then the mode set fails with one error naming the mode store path and its cause
    And the owned mode store is as it was

    Examples:
      | store                                  | target |
      | holds "off" in a locked mode file      | on     |
      | holds "on" in a locked mode file       | off    |
      | folder's place is taken by a file      | on     |
      | folder's place is taken by a file      | off    |
