#!/usr/bin/env python3
"""Copy the canonical project runner to its single derived example."""
import argparse
import os
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SOURCE = Path(__file__).resolve().with_name("gap_check.py")
DEFAULT_TARGET = ROOT / "dydo/reference/gap-check.example.py"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--target", type=Path, default=DEFAULT_TARGET)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    source, target = args.source.resolve(), args.target.resolve()
    if not source.is_file():
        print(f"canonical runner is missing: {source}")
        return 2
    expected = source.read_bytes()
    if args.check:
        if not target.is_file() or target.read_bytes() != expected:
            print(f"derived runner differs: {target}")
            return 2
        return 0
    target.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=target.name + ".", suffix=".tmp", dir=target.parent)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(expected)
            stream.flush()
            os.fsync(stream.fileno())
        Path(temporary).replace(target)
    except BaseException:
        Path(temporary).unlink(missing_ok=True)
        raise
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
