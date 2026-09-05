@DYD-113
Feature: One project-local interface runs tests and assurance honestly
  Agents can select tests and hard gates without memorizing each stack's commands.
  Missing project capabilities remain visible and cannot become a successful gate.

  Scenario: Help describes the complete stable interface
    Given a valid project testing manifest
    When I request the project testing runner help
    Then the command succeeds without running a configured command
    And help names test, all, gate static, gate coverage, gate mutation, capabilities and --force-run
    And help explains stack selection, defaults, native test arguments, result artifacts and exit codes 0, 1 and 2

  Scenario: Bare invocation is useful but is not a test or gate
    Given a valid project testing manifest
    When I invoke the project testing runner without arguments
    Then help is printed
    And no configured command runs
    And no result artifact is created
    And the command exits 2

  Scenario: Run one stack's selected tests without hidden gates
    Given the "dotnet" stack has a configured test command and configured hard gates
    When I run "test --stack dotnet -- --filter FullyQualifiedName~ParserTests"
    Then only the dotnet test command runs
    And the native arguments "--filter FullyQualifiedName~ParserTests" are appended as separate argv items
    And static, coverage and mutation commands do not run

  Scenario: Run all configured tests for selected stacks
    Given configured tests for the "dotnet", "frontend" and "python" stacks
    When I run "all --stack frontend,python"
    Then the frontend and python test commands each run once in manifest order
    And the dotnet test command does not run
    And no static, coverage or mutation command runs

  Scenario: Run every configured test by default
    Given configured tests and an unavailable test capability in the manifest
    When I run "all" without stack selection
    Then every configured test command runs once in manifest order
    And the unavailable test capability is reported by capabilities but is not selected
    And no static, coverage or mutation command runs

  Scenario Outline: Hard-gate operations stay separate
    Given the selected stack has configured static, coverage and mutation commands
    When I run "<invocation>"
    Then only the "<capability>" command runs for that stack
    And the result names the candidate, argv, working directory, isolation, artifacts and capability state

    Examples:
      | invocation                                      | capability |
      | gate static --stack dotnet                      | static     |
      | gate coverage --stack dotnet                    | coverage   |
      | gate mutation --since BASE --stack dotnet       | mutation   |

  Scenario: Mutation requires a comparison base
    Given the selected stack has a configured mutation command
    When I run "gate mutation --stack dotnet" without --since
    Then no configured command runs
    And the diagnostic requires --since BASE
    And the command exits 2

  Scenario: Capabilities report configuration without running it
    Given configured and unavailable capabilities in the manifest
    When I run "capabilities"
    Then no configured command runs
    And every stack and test, static, coverage and mutation state is reported with its reason when unavailable
    And the command succeeds

  Scenario: Full-G compatibility never degrades to tests only
    Given all configured tests pass
    And an applicable static or coverage capability is unavailable
    When I run "--force-run"
    Then all configured tests and every configured applicable static and coverage command run
    And mutation does not run
    And every unavailable applicable static or coverage capability is reported
    And the command exits 2

  Scenario: Independent work is exhausted before aggregation
    Given selected configured commands that pass and fail and a selected unavailable capability
    When I run the requested operation
    Then every independent selected result is reported before the aggregate result
    And the command exits 2 because unavailable outranks a measured failure

  Scenario Outline: Results preserve the meaning of the exit code
    Given a recognized operation whose selected work is "<outcome>"
    When the operation completes
    Then the result artifact records aggregate exit <exit>
    And the command exits <exit>

    Examples:
      | outcome                                                        | exit |
      | all configured work passed                                     | 0    |
      | a test or policy measurement failed and no work was unavailable | 1    |
      | invalid, missing, malformed, unsupported or unavailable         | 2    |

  Scenario: A started operation leaves one machine-readable result
    Given a recognized operation and a valid artifact root
    When the operation completes with pass, failure or unavailability
    Then one JSON result is written beneath a unique run directory
    And it records schema 1, candidate commit and dirty state, operation, selected stacks, ordered results and aggregate exit
    And each result records stack, capability, state, argv, working directory, isolation, child exit, artifacts and reason as applicable
    And the human-readable summary prints that result path

  Scenario Outline: Invalid requests fail closed before execution
    Given a project testing manifest with "<problem>"
    When I invoke an operation that depends on it
    Then no configured command runs
    And the diagnostic identifies "<problem>"
    And the command exits 2

    Examples:
      | problem                                      |
      | an unknown schema version                    |
      | an unknown selected stack                    |
      | an undeclared capability                     |
      | an unavailable selected capability           |
      | an empty command vector                       |
      | a command vector containing an angle placeholder |
      | a missing executable                         |
      | malformed configured evidence                |

  Scenario: The portable three-stack example is visibly unfinished
    Given the canonical portable testing manifest
    Then it declares ASP.NET, React/Vite and Python/uv stacks
    And its test argv vectors invoke dotnet test, Node with Vitest, and uv run --locked -m pytest
    And its isolation modes are git-worktree with copied working changes, per-run artifacts, and in-place
    And project paths and the frontend artifact variable remain visible angle placeholders
    And static, coverage and mutation are unavailable with adoption reasons
    When I run "all" with the untouched portable example
    Then no configured command runs
    And the command exits 2

  Scenario: DynaDocs uses its real adapters and admits missing assurance
    Given the DynaDocs project testing manifest
    Then its dotnet test command invokes DynaDocs.Tests/coverage/run_tests.py with the current Python interpreter
    And its Python test command invokes unittest discovery for the facade conformance tests
    And its Node test command invokes node --test for the facade conformance test
    And static and coverage are unavailable pending DYD-96
    And mutation is unavailable pending DYD-103
    And its artifact root is DynaDocs.Tests/coverage/results

  Scenario: The project and portable runners cannot drift apart
    Given the DynaDocs runner and the canonical portable runner
    Then their source bytes are identical
    And the same conformance suite verifies their grammar, manifest validation, execution order, result schema and exit aggregation
    And Node conformance exercises real child argv, standard streams and exit propagation through the configured Node adapter
