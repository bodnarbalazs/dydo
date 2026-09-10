"""Normalize real mutation evidence into the schema-1 summary the mutation adapter publishes.

Three stages, each pure data in and data out, so a captured report replays anywhere:

1. `read_stryker_report` / `read_cosmic_session` read one engine's own evidence and return
   `{"projectRoot", "files", "rows", "gaps"}`, where every row is
   `{"path", "id", "mutator", "native", "status", "span", "raw"}` with the engine's own path
   spelling, the specification's normalized status, and
   `span = {"startLine", "startColumn", "endLine", "endColumn"}` exactly as the engine reported it.
2. `map_report_paths` maps every vendor path onto the canonical inventory spelling against an
   explicitly supplied snapshot root -- never `cwd`, the environment or a module constant.
3. `normalize` applies the policy: the ordered report validation, the counts, one finding per
   non-killed valid mutant, the gaps, the score and the exit code.

`--read-cosmic-session` is the one subcommand the adapter runs out of process, because a Cosmic Ray
session is a session database rather than a report file; it takes the campaign's marker nonce
explicitly, exactly as the readers take the snapshot root.
"""
import argparse
import json
import sys
from pathlib import Path


def read_stryker_report(path, report=None):
    """Read one Stryker.NET or StrykerJS mutation-testing-report JSON file.

    `report` is the label recorded in each row's `raw`, defaulting to the file's name.
    """
    return {"projectRoot": None, "files": [], "rows": [], "gaps": []}  # step 5 fills this.


def read_cosmic_session(path, marker_nonce, report=None):
    """Read one Cosmic Ray session, reading each `killed` row against `marker_nonce`."""
    return {"projectRoot": None, "files": [], "rows": [], "gaps": []}  # step 5 fills this.


def map_report_paths(reading, snapshot_root, inventory_paths):
    """Map every vendor path in a reading onto the one inventory path it resolves to."""
    return {"projectRoot": None, "files": [], "rows": [], "gaps": []}  # step 5 fills this.


def normalize(reading, selected, engine, substantive=True):
    """Apply the mutation policy to a mapped reading of one campaign."""
    return {"counts": {}, "findings": [], "gaps": [], "score": None, "witness": [],
            "measurementComplete": False, "exitCode": 0}  # step 5 fills this.


def main(argv=None):
    """--read-cosmic-session <session> --marker-nonce <nonce> --output <json>."""
    return 0  # step 5 fills this.


if __name__ == "__main__":
    sys.exit(main())
