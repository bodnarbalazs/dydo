# Initialize and check documentation

## Sub-features

`Commands/InitCommand.cs` scaffolds the docs and installs the chosen host's guard.
`Commands/CheckCommand.cs` validates the documentation graph. Existing init and check acceptance
scenarios under `DynaDocs.Tests/Features/` remain the broader contract.

## How to get to it (user POV)

In a new project run `dydo init claude`, then `dydo check`. The driver creates that project during
launch, using the built candidate instead of a globally installed executable.

## Driving it with control-dydo

`drive --run RUN --journey init-docs` checks the scaffold, introduces one broken link, observes
the nonzero check and named target, removes that fixture page, then observes a successful check.
Capture `init`, all three `check-*` receipts and `init-side-effects.json` for generated files.

## Gotchas

The mock project must be outside the candidate checkout: ancestor `dydo.json` discovery would
otherwise refuse initialization. This journey covers Claude installation only; Codex/global
configuration and `--join` need separate mapped drives before anyone claims their coverage.
