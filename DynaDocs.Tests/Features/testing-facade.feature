@DYD-113
Feature: One project-local interface runs tests and assurance honestly
  Agents can select tests and hard gates without memorizing each stack's commands.
  Missing project capabilities remain visible and cannot become a successful gate.

  Scenario: Help describes the complete stable interface
    Given a valid project testing manifest
    When I request the project testing runner help
    Then the command succeeds without running a configured command
    And help names test, all, gate static, gate coverage, gate mutation, capabilities and --force-run
    And help explains stack selection, defaults, native test arguments, result artifacts, exit 0 for pass, 1 for measured failure, 2 for invalid or unavailable work, and 130 for interruption after adapter cleanup

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
    Given configured, unavailable and invalid test rows in the manifest
    When I run "all" without stack selection
    Then every declared test row is selected in manifest order
    And every valid configured test command runs once
    And unavailable and invalid test rows are reported without running their commands
    And no static, coverage or mutation command runs
    And the command exits 2

  Scenario Outline: Hard-gate operations stay separate
    Given the selected stack has configured static, coverage and mutation commands
    When I run "<invocation>"
    Then only the "<capability>" command runs for that stack
    And the result names the candidate, argv, working directory, isolation requirement, adapter evidence, artifacts and capability state

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

  Scenario: A project adapter places the mutation base in its native argv
    Given exactly one argv element in the selected mutation command equals "{base}"
    When I run "gate mutation --since BASE --stack dotnet"
    Then "BASE" replaces that whole element as one unchanged argv item at that position
    And the runner does not add another --since option
    And the command runs without a shell

  Scenario Outline: An ambiguous mutation base contract fails closed
    Given the selected configured mutation command has "<problem>"
    When I run "gate mutation --since BASE --stack dotnet"
    Then that mutation command does not run
    And independent valid selected commands still run
    And the diagnostic requires exactly one argv element equal to "{base}" and no substring occurrence
    And the command exits 2

    Examples:
      | problem                                      |
      | no argv element equal to {base}              |
      | two argv elements equal to {base}             |
      | an argv element containing --baseline={base}  |

  Scenario: Capabilities report configuration without running it
    Given configured and unavailable capabilities in the manifest
    When I run "capabilities"
    Then no configured command runs
    And every stack and test, static, coverage and mutation state is reported with its reason when unavailable
    And the command succeeds

  Scenario: Full-G compatibility never degrades to tests only
    Given valid, unavailable and invalid test, static and coverage rows
    When I run "--force-run"
    Then every declared test, static and coverage row is selected in manifest order
    And every valid configured selected command runs except a test row whose stack declares a coverage suite verdict: it runs no command of its own and its verdict comes from that one instrumented execution
    And unavailable and invalid selected rows are reported without running their commands
    And mutation does not run
    And the command exits 2

  Scenario: One instrumented execution carries both the coverage measurement and the test verdict
    Given a stack whose coverage row declares a suite verdict and publishes a passing suite exit
    When I run "--force-run"
    Then the derived test row is passed with childExit 0 and resultExit 0
    And the coverage row is passed with resultExit 0
    And the aggregate exit is 0

  Scenario Outline: A derived test verdict fails closed
    Given a stack whose coverage row declares a suite verdict with "<case>"
    When I run "--force-run"
    Then the derived test row is <state> with childExit <childExit> and resultExit <resultExit>
    And the coverage row is <coverageState> with resultExit <coverageExit>
    And the aggregate exit is <aggregate>

    Examples:
      | case                                                          | state   | childExit | resultExit | coverageState | coverageExit | aggregate |
      | suite passes and policy passes                                | passed  | 0         | 0          | passed        | 0            | 0         |
      | suite passes and policy fails                                 | passed  | 0         | 0          | failed        | 1            | 1         |
      | suite fails                                                   | failed  | 5         | 1          | failed        | 1            | 1         |
      | the campaign could not measure                                | invalid | null      | 2          | invalid       | 2            | 2         |
      | the campaign is invalid but its report records the suite exit | invalid | null      | 2          | invalid       | 2            | 2         |
      | the report records no suite exit                              | invalid | null      | 2          | passed        | 0            | 2         |
      | the coverage row did not attribute the child exit to the suite | invalid | null     | 2          | failed        | 1            | 2         |

  Scenario: An interruption before the coverage row leaves no unresolved test verdict
    Given a declaring stack whose static row waits for an interrupt
    When I interrupt the run after the static row starts
    Then the derived test row is interrupted with childExit null and resultExit 130
    And the aggregate exit is 130

  Scenario Outline: Derivation belongs to the invocation, not to the coverage row
    Given a declared suite verdict stack whose coverage command and test command are observed
    When I run the invocation "<invocation>"
    Then the test command <testRuns> and the coverage command <coverageRuns>

    Examples:
      | invocation                  | testRuns     | coverageRuns |
      | all                         | runs         | does not run |
      | test --stack dotnet         | runs         | does not run |
      | gate coverage --stack dotnet | does not run | runs         |
      | --force-run                 | does not run | runs         |

  Scenario: A stack without a declared suite verdict keeps its own test execution
    Given a configured coverage row without a declaration and a stack whose coverage row is unavailable
    When I run "--force-run"
    Then both plain test rows run and record their own child exits

  Scenario: Independent work is exhausted before aggregation
    Given selected rows that pass, fail, are invalid and are unavailable
    When I run the requested operation
    Then every valid configured selected command runs
    And each invalid or unavailable row alone is skipped and reported
    And every independent selected result is reported before the aggregate result
    And the command exits 2 because unavailable outranks a measured failure

  Scenario Outline: Globally invalid input prevents all execution
    Given the request or manifest has "<problem>"
    When I invoke the project testing runner
    Then no configured command runs
    And the diagnostic identifies "<problem>"
    And the command exits 2

    Examples:
      | problem                              |
      | invalid JSON                         |
      | a schema value other than 1          |
      | a missing top-level field            |
      | a non-array stacks field             |
      | duplicate stack names                |
      | an unknown selected stack            |
      | invalid operation syntax              |
      | mutation without --since BASE        |

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
      | an interrupted adapter completed its cleanup                    | 130  |

  Scenario: A started operation leaves one machine-readable result
    Given a recognized operation and a valid artifact root
    When the operation completes with pass, failure, unavailability or interruption
    Then one JSON result is written beneath a unique run directory
    And it records schema 1, candidate commit and dirty state, operation, selected stacks, ordered results and aggregate exit
    And each result records stack, capability, state, argv, working directory, isolation requirement, adapter evidence, child exit, artifacts and reason as applicable
    And the human-readable summary prints that result path

  Scenario: Schema 1 has one small concrete manifest and result shape
    Given this schema 1 manifest shape:
      """
      {
        "schema": 1,
        "artifactRoot": "DynaDocs.Tests/coverage/results",
        "stacks": [{
          "name": "dotnet",
          "kind": "aspnet",
          "cwd": ".",
          "isolation": {
            "requirement": "git-worktree-copy-working-changes",
            "evidence": {"state": "verified", "kind": "adapter", "path": "DynaDocs.Tests/coverage/run_tests.py"}
          },
          "capabilities": {
            "test": {"state": "configured", "command": {"kind": "current-python", "argv": ["-u", "DynaDocs.Tests/coverage/run_tests.py", "--"]}, "artifacts": []},
            "static": {"state": "configured", "command": {"kind": "current-python", "argv": ["DynaDocs.Tests/coverage/gate_adapter.py", "--stack", "dotnet", "--gate", "static"]}, "artifacts": [{"path": "DynaDocs.Tests/coverage/results/adapters/dotnet-static.json", "required": true}]},
            "coverage": {"state": "configured", "command": {"kind": "current-python", "argv": ["DynaDocs.Tests/coverage/gate_adapter.py", "--stack", "dotnet", "--gate", "coverage"]}, "artifacts": [{"path": "DynaDocs.Tests/coverage/results/adapters/dotnet-coverage.json", "required": true}], "suiteVerdict": {"exit": ["collectors", "csharp-coverage", "facts", "child_exit"], "failure": ["collectors", "csharp-coverage", "findings", {"gate": "functional"}]}},
            "mutation": {"state": "configured", "command": {"kind": "current-python", "argv": ["DynaDocs.Tests/coverage/mutation_adapter.py", "--stack", "dotnet", "--since", "{base}"]}, "artifacts": [{"path": "DynaDocs.Tests/coverage/results/adapters/dotnet-mutation.json", "required": true}]}
          }
        }]
      }
      """
    Then schema is the integer 1, artifactRoot is repository-relative, and stacks is an ordered nonempty array with unique nonempty names
    And every stack has exactly name, kind, cwd, isolation and capabilities
    And isolation requirement is in-place, git-worktree-copy-working-changes or per-run-artifacts
    And capabilities has exactly test, static, coverage and mutation rows
    And cwd, adapter paths, artifactRoot and declared artifact paths resolve inside the repository root without absolute paths or parent escape
    And command kind is argv or current-python with a nonempty array of string argv items
    And current-python prefixes argv with sys.executable while argv commands execute their array unchanged
    And configured rows require command and artifacts and forbid reason while unavailable rows require reason, forbid command and artifacts, and may carry non-executable exampleArgv, and a configured coverage row may additionally declare suiteVerdict with exactly exit and failure and exactly one required artifact
    And a derived test row records empty argv, the suite's own child exit and a reason naming the coverage report
    And artifacts is an array of objects with repository-relative path and required boolean
    And non-test configured gates declare at least one required artifact whose normalized path exists after a successful child exit
    And verified in-place evidence has kind direct while every other verified isolation requirement has kind adapter and an existing repository-contained path
    And unavailable isolation evidence has a nonempty reason and makes that stack's selected row unavailable
    And the result shape is schema, candidate, operation, selectedStacks, results and aggregateExit
    And candidate is commit and dirty, operation is name and optional since, and selectedStacks preserves manifest order
    And each result is stack, capability, state, argv, cwd, isolation, childExit, resultExit, artifacts and optional reason
    And state is passed, failed, unavailable, invalid or interrupted; childExit is the raw integer or null; resultExit is 0, 1, 2 or 130

  Scenario Outline: A row-local defect skips only that row
    Given selected independent rows and one row with "<problem>"
    When I invoke an operation that selects them
    Then the defective row does not run and is reported as invalid or unavailable
    And every independent valid selected row still runs
    And the command exits 2 after all selected rows are reported

    Examples:
      | problem                                           |
      | an undeclared capability                          |
      | an unavailable selected capability                |
      | an empty command vector                           |
      | a command vector containing an angle placeholder  |
      | a missing executable                              |
      | malformed configured evidence                     |
      | a cwd or artifact path escaping the repository    |
      | an invalid suite verdict declaration              |

  Scenario: The portable three-stack example is visibly unfinished
    Given the canonical portable testing manifest
    Then it declares ASP.NET, React/Vite and Python/uv stacks
    And its prospective ASP.NET and React/Vite argv vectors show dotnet test and Node with Vitest
    And its prospective Python/uv argv vector is exactly these items in order:
      | item     |
      | uv       |
      | run      |
      | --locked |
      | --extra  |
      | dev      |
      | -m       |
      | pytest   |
    And ASP.NET requires worktree isolation with copied working changes but its adapter evidence is unavailable
    And the frontend and Python isolation requirements are per-run artifacts and in-place
    And project paths and the frontend artifact variable remain visible angle placeholders
    And static, coverage and mutation are unavailable with adoption reasons
    When I run "all" with the untouched portable example
    Then no configured command runs
    And every declared test row is reported as invalid or unavailable
    And the command exits 2

  Scenario: Partial adoption runs valid peers without hiding remaining work
    Given the portable frontend test row is fully adapted
    And the ASP.NET test row remains unavailable and the Python test row remains invalid
    When I run "all" from the portable example
    Then the frontend test command runs
    And the ASP.NET and Python test rows are reported without running
    And the command exits 2

  Scenario: Partial adoption permits a fully adapted targeted test
    Given the portable frontend test row is fully adapted
    And unrelated rows remain unavailable or invalid
    When I run "test --stack frontend"
    Then only the frontend test command runs
    And the command succeeds

  Scenario: DynaDocs uses its real adapters and admits missing assurance
    Given the DynaDocs project testing manifest
    Then its dotnet test command invokes DynaDocs.Tests/coverage/run_tests.py with the current Python interpreter
    And dotnet requires worktree isolation with copied working changes and names run_tests.py as verified adapter evidence
    And its Python test command invokes unittest discovery for the facade conformance tests
    And its Node test command invokes node --test for the facade conformance test
    And static and coverage are unavailable pending DYD-96
    And each stack's coverage row declares the suite verdict its test row is derived from
    And mutation is unavailable pending DYD-103
    And its artifact root is DynaDocs.Tests/coverage/results

  Scenario: Interrupting the real isolated dotnet adapter leaves no worktree behind
    Given the DynaDocs dotnet test row runs the actual run_tests.py adapter with unbuffered current Python
    When the facade observes the adapter's registered temporary worktree path and receives an interrupt
    Then the facade sends the platform-appropriate interrupt to the adapter process group without immediately killing the wrapper
    And it allows up to 30 seconds for run_tests.py and dotnet to unwind through the wrapper's finally cleanup before escalation
    And the reported temporary path no longer exists
    And git worktree list no longer contains the temporary path
    And the result row has state interrupted, raw childExit or null, and resultExit 130
    And the result artifact has aggregateExit 130 and is printed after cleanup
    And the facade exits 130

  Scenario: The project and portable runners cannot drift apart
    Given the DynaDocs runner and the canonical portable runner
    Then their source bytes are identical
    And the same conformance suite verifies their grammar, manifest validation, execution order, result schema and exit aggregation
    And Node conformance exercises real child argv, standard streams and exit propagation through the configured Node adapter

  Scenario: Node conformance preserves observable process behavior
    Given a configured Node fixture command and no unavailable selected peer
    When I run "test --stack node -- -- 'árvíztűrő tükörfúrógép'"
    Then the fixture receives the literal argv elements "--" and "árvíztűrő tükörfúrógép" in order
    And standard output contains "NODE_STDOUT_SENTINEL"
    And standard error contains "NODE_STDERR_SENTINEL"
    And the fixture child exit 17 is recorded unchanged
    And the Node result has state failed and resultExit 1
    And the result artifact has aggregateExit 1
    And the facade exits 1
