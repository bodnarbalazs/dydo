5. Run the candidate's exact test commands through `DynaDocs.Tests/coverage/run_tests.py`, never
   `dotnet test` directly.
6. Run `python DynaDocs.Tests/coverage/gap_check.py --force-run`. A non-zero result is a finding.
