"""The exclusive mutation adapter: one real campaign per stack over an isolated snapshot.

`mutation_adapter.py --stack <name> --since <base>` takes the mutation slot, snapshots the
caller's committed, dirty and untracked content, produces the inventory inside that snapshot,
selects the changed targets of one stack, runs Stryker.NET, StrykerJS or Cosmic Ray under
`windows_job`, normalizes the evidence through `mutation_summary` and publishes one schema-1
summary. Exit 0 when every valid generated mutant was killed, 1 for a measured finding, 2 for
measurement that is missing, invalid or stale, and 130 for an interruption whose owned cleanup
completed.
"""
import sys

import inventory
import mutation_summary
import run_tests
import windows_job

# The build-configuration paths that widen a stack, verbatim from the specification.
CONFIG_WIDENING = {}  # step 6 fills this.

# The stack whose campaign each inventory language belongs to.
STACK_LANGUAGE = {}  # step 6 fills this.

# The generated in-process Cosmic Ray suite runner, with this campaign's nonce substituted.
SUITE_RUNNER_TEMPLATE = ""  # step 6 fills this.


def select(candidate, changed, stack):
    """Return `{mode, reason, changedTargets, selected, witness, gaps}` for one stack.

    The first five keys are the published `selection`; `gaps` are the selection-time refusals -- a
    stale inventory, an unclassified source, an unselectable applicable target.
    """
    return {"mode": "", "reason": "", "changedTargets": [], "selected": [], "witness": [],
            "gaps": []}  # step 6 fills this.


def render_suite_runner(nonce):
    """Return the generated suite runner carrying `nonce` as its embedded marker literal."""
    return ""  # step 6 fills this.


def render_cosmic_test_command(executable, runner, argv):
    """Return the Cosmic Ray `test-command` value for one campaign."""
    return ""  # step 6 fills this.


def validate_template(stack, template):
    """Return the gaps of one campaign template before anything is generated from it."""
    return []  # step 6 fills this.


def main(argv=None):
    """Run one stack's campaign and publish its summary."""
    return 0  # step 6 fills this.


if __name__ == "__main__":
    sys.exit(main())
