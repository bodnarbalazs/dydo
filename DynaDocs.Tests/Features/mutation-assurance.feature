@DYD-103
Feature: Mutation assurance runs real campaigns over isolated changed code
  The separate gate mutation operation runs Stryker.NET, StrykerJS and Cosmic Ray over an isolated
  snapshot of the candidate, selected from DYD-96's inventory and the diff against the base.
  Every valid generated mutant must be killed; missing or invalid measurement can never pass.

  Scenario: Capabilities report the configured mutation rows without running them
    Given the DynaDocs project testing manifest
    When I run "capabilities"
    Then dotnet, python and node each report mutation configured
    And no configured command runs
    And the command succeeds

  Scenario Outline: Each stack's mutation row names the exclusive adapter and its base placeholder
    Given the DynaDocs project testing manifest
    Then the "<stack>" mutation command is current-python with argv DynaDocs.Tests/coverage/mutation_adapter.py --stack <stack> --since {base}
    And its only required artifact is "<artifact>"

    Examples:
      | stack  | artifact                                                  |
      | dotnet | DynaDocs.Tests/coverage/results/adapters/dotnet-mutation.json |
      | python | DynaDocs.Tests/coverage/results/adapters/python-mutation.json |
      | node   | DynaDocs.Tests/coverage/results/adapters/node-mutation.json   |

  Scenario: Full-G compatibility still never runs mutation
    Given a manifest whose three mutation rows are configured with the real adapter
    When I run "--force-run"
    Then every test, static and coverage row is selected in manifest order
    And no mutation adapter starts and no mutation summary is written

  Scenario Outline: A campaign whose valid mutants are all killed passes
    Given a candidate with one changed executable "<stack>" target since BASE
    And the "<stack>" engine reports every valid generated mutant as killed
    When I run "gate mutation --since BASE --stack <stack>"
    Then the "<stack>" row has state passed, childExit 0 and resultExit 0
    And the summary records gate mutation, the candidate commit, dirty state and source fingerprint, the inventory path and hash, tool versions, ordered commands with raw exits, and raw report paths with hashes
    And the summary selection mode is changed and names the changed target
    And the summary counts killed equals valid, score is 100 and measurementComplete is true
    And the summary has no finding and no gap

    Examples:
      | stack  |
      | dotnet |
      | python |
      | node   |

  Scenario Outline: A mutant that is not killed is a measured finding
    Given a candidate with one changed executable "<stack>" target since BASE
    And the "<stack>" engine reports one mutant with native status "<native>"
    When I run "gate mutation --since BASE --stack <stack>"
    Then the "<stack>" row has state failed, childExit 1 and resultExit 1
    And the summary has exactly one finding with status "<normalized>" naming the path, span, mutator and raw report reference
    And measurementComplete is true and the command exits 1

    Examples:
      | stack  | native                              | normalized   |
      | dotnet | Survived                            | survived     |
      | dotnet | NoCoverage                          | noCoverage   |
      | dotnet | Timeout                             | timeout      |
      | dotnet | RuntimeError                        | runtimeError |
      | dotnet | Ignored                             | ignored      |
      | dotnet | Pending                             | unrun        |
      | node   | Survived                            | survived     |
      | node   | Timeout                             | timeout      |
      | node   | RuntimeError                        | runtimeError |
      | python | survived                            | survived     |
      | python | killed without a suite completion line | timeout   |
      | python | skipped                             | unrun        |

  Scenario Outline: Missing or invalid measurement is invalid, never a pass
    Given a candidate with one changed executable "<stack>" target since BASE
    And the campaign has "<problem>"
    When I run "gate mutation --since BASE --stack <stack>"
    Then the "<stack>" row has state invalid, childExit 2 and resultExit 2
    And the summary gap names "<reason>"
    And measurementComplete is false and the command exits 2

    Examples:
      | stack  | problem                                                | reason                                                |
      | dotnet | the engine is not restored                             | dotnet tool restore                                   |
      | node   | the engine is not restored                             | npm --prefix DynaDocs.Tests/coverage/mutation ci --ignore-scripts |
      | python | the engine is not restored                             | pip install --no-deps -r DynaDocs.Tests/coverage/mutation/requirements.lock |
      | python | a failing baseline test run                            | baseline test run failed                              |
      | node   | a failing baseline test run                            | baseline test run failed                              |
      | dotnet | a nonzero engine exit without a report                 | no mutation report produced                           |
      | dotnet | a malformed report                                     | malformed report                                      |
      | node   | a report that omits the selected file                  | partial report                                        |
      | python | a session whose work item has no result                | partial report                                        |
      | python | a worker outcome of exception                          | engine could not run mutant                           |
      | dotnet | a source file changed by the engine and not restored   | candidate changed during the campaign                 |
      | node   | zero generated mutants                                 | zero-mutant campaign                                  |
      | dotnet | every generated mutant a compile error                 | all mutants invalid                                   |
      | python | an existing summary lock                               | mutation slot busy                                    |
      | dotnet | a base that no commit resolves                         | unresolvable base                                     |
      | node   | a base that is not an ancestor of the candidate        | base is not an ancestor                               |
      | dotnet | an inventory with nonempty errors                      | inventory errors                                      |
      | dotnet | a changed C# target outside DynaDocs.csproj            | no .NET test project route                            |
      | node   | a changed JavaScript target without an extension       | extensionless target                                  |
      | dotnet | a template whose concurrency is 2                      | invalid mutation configuration                        |

  Scenario: A change touching no maintained source passes as a witnessed no-op
    Given the only change since BASE is a Markdown file
    When I run "gate mutation --since BASE"
    Then every stack row has state passed and resultExit 0
    And no engine starts
    And each summary records selection mode none, an empty changedTargets, zero counts and a witness naming the Markdown path as no obligation
    And measurementComplete is true

  Scenario Outline: Uncertain selection widens to the whole stack
    Given a "<stack>" candidate whose change since BASE is "<change>"
    When I run "gate mutation --since BASE --stack <stack>"
    Then the summary selection mode is widened with reason "<reason>"
    And the engine receives every maintained "<stack>" target and nothing narrower

    Examples:
      | stack  | change                                          | reason                                |
      | dotnet | a renamed executable target                     | deleted or renamed source             |
      | python | a deleted executable target                     | deleted or renamed source             |
      | node   | a test file that no target lists                | test change with no associated target |
      | dotnet | the .config/dotnet-tools.json manifest          | configuration changed                 |

  Scenario: A test-only change reruns exactly the targets that list it
    Given a python candidate whose only change since BASE is a test file listed by two targets
    When I run "gate mutation --since BASE --stack python"
    Then the summary selection mode is changed and changedTargets are exactly those two targets
    And the engine receives exactly those two files

  Scenario: Dirty and untracked content is measured without touching the caller's tree
    Given a candidate with an uncommitted modification and an untracked new executable target
    When I run "gate mutation --since BASE --stack python"
    Then the inventory and the campaign observe both files inside a snapshot outside the caller's tree
    And the caller's tree is byte-identical after the run
    And the snapshot path no longer exists and git worktree list no longer registers it

  Scenario Outline: Interruption completes owned cleanup before 130
    Given a "<stack>" campaign whose engine is long-lived
    When the facade receives an interrupt during that campaign
    Then the engine process tree is gone before the row is reported
    And the snapshot is removed and unregistered and the summary lock is released
    And the run report records exitCode 130 and no summary is published
    And the "<stack>" row has state interrupted and resultExit 130 and the command exits 130

    Examples:
      | stack  |
      | dotnet |
      | python |
      | node   |

  Scenario Outline: Campaign settings are pinned in the templates
    Given the "<template>" template under DynaDocs.Tests/coverage/mutation
    Then its "<setting>" is "<value>"
    And a template with any other value for it makes the campaign invalid

    Examples:
      | template         | setting          | value       |
      | stryker-net.json | concurrency      | 1           |
      | stryker-net.json | thresholds       | 100/100/100 |
      | stryker-net.json | reporters        | json,html   |
      | stryker-js.json  | coverageAnalysis | off         |
      | stryker-js.json  | concurrency      | 1           |
      | stryker-js.json  | inPlace          | true        |
      | cosmic-ray.toml  | distributor      | local       |
