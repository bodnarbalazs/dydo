#!/usr/bin/env python3
"""Render current normalized static and coverage adapter summaries."""
import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "DynaDocs.Tests/coverage/results/adapters"


def render(summary):
    complete = "complete" if summary["measurementComplete"] else "incomplete"
    lines = [f"{summary['stack']} {summary['gate']}: exit {summary['exitCode']} ({complete})"]
    for finding in summary["findings"]:
        member = f" [{finding['member']}]" if finding.get("member") else ""
        lines.append(f"  {finding['gate']}: {finding.get('path', '<project>')}{member}")
    for gap in summary["gaps"]:
        lines.append(f"  gap: {gap.get('collector', '<collector>')}: {gap.get('message', gap.get('reason', 'missing evidence'))}")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="*", type=Path)
    args = parser.parse_args()
    paths = args.paths or sorted(DEFAULT.glob("*.json"))
    if not paths:
        print("No adapter summaries found")
        return 2
    code = 0
    for path in paths:
        try:
            summary = json.loads(path.read_text(encoding="utf-8"))
            print(render(summary))
            code = max(code, summary["exitCode"])
        except (OSError, ValueError, KeyError, TypeError) as error:
            print(f"invalid summary {path}: {error}")
            code = 2
    return code


if __name__ == "__main__":
    raise SystemExit(main())
