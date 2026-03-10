#!/usr/bin/env python3
"""Tier compliance checker.

Runs tests, collects coverage data, and checks every source module against
its tier's requirements.

Coverage: Cobertura XML via Coverlet (includes cyclomatic complexity).

Usage:
    python DynaDocs.Tests/coverage/gap_check.py                    # run tests and check
    python DynaDocs.Tests/coverage/gap_check.py --skip-tests       # analyze existing data only
    python DynaDocs.Tests/coverage/gap_check.py --detail            # show uncovered lines
    python DynaDocs.Tests/coverage/gap_check.py --inspect Guard     # inspect matching modules
"""

import argparse
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

ROOT = Path(__file__).resolve().parent.parent.parent
COVERAGE_DIR = ROOT / "DynaDocs.Tests" / "coverage"
RUNSETTINGS = COVERAGE_DIR / "coverage.runsettings"
TIER_REGISTRY_PATH = COVERAGE_DIR / "tier_registry.json"

XML_PATTERN = "DynaDocs.Tests/**/coverage.cobertura.xml"

TEST_COMMAND = [
    "dotnet", "test", "DynaDocs.sln",
    "--collect:XPlat Code Coverage",
    f"--settings:{RUNSETTINGS}",
]

EXCLUDED_CLASSES = {"Program"}
DATA_MODEL_MAX_LINES = 3
GENERATED_PATTERNS = ["/obj/", ".g.cs", ".generated.cs"]

TIER_THRESHOLDS = {
    1: {"line_coverage": 0.80, "crap": 30, "branch_coverage": 0.60},
    2: {"line_coverage": 1.00, "crap": 15, "branch_coverage": 0.80},
    3: {"line_coverage": 1.00, "crap": 5,  "branch_coverage": 1.00},
}

TIER_ANNOTATION_RE = re.compile(r"//\s*@test-tier:\s*(\d+)")
CONDITION_COVERAGE_RE = re.compile(r"\((\d+)/(\d+)\)")


# ---------------------------------------------------------------------------
# Data models
# ---------------------------------------------------------------------------

@dataclass
class LineLevelData:
    """Per-line hit counts for merging overlapping coverage."""
    lines_hits: Dict[int, int] = field(default_factory=dict)
    branch_conditions: Dict[int, Tuple[int, int]] = field(default_factory=dict)
    class_name: str = ""
    complexity: float = 0.0

    def merge(self, line_no: int, hits: int):
        self.lines_hits[line_no] = max(self.lines_hits.get(line_no, 0), hits)

    def merge_branch(self, line_no: int, covered: int, total: int):
        existing = self.branch_conditions.get(line_no)
        if existing is None:
            self.branch_conditions[line_no] = (covered, total)
        else:
            self.branch_conditions[line_no] = (max(existing[0], covered), total)

    def merge_complexity(self, cc: float):
        self.complexity = max(self.complexity, cc)

    @property
    def lines_valid(self) -> int:
        return len(self.lines_hits)

    @property
    def lines_covered(self) -> int:
        return sum(1 for h in self.lines_hits.values() if h > 0)

    @property
    def line_rate(self) -> float:
        if self.lines_valid == 0:
            return 0.0
        return self.lines_covered / self.lines_valid

    @property
    def branch_rate(self) -> float:
        if not self.branch_conditions:
            return 1.0
        total_covered = sum(c for c, _ in self.branch_conditions.values())
        total_possible = sum(t for _, t in self.branch_conditions.values())
        if total_possible == 0:
            return 1.0
        return total_covered / total_possible


@dataclass
class ModuleCoverage:
    """Coverage data for a single source module (file)."""
    filename: str
    class_name: str
    line_rate: float
    branch_rate: float
    lines_valid: int
    lines_covered: int
    complexity: float
    tier: int = 1
    line_hits: Dict[int, int] = field(default_factory=dict)
    branch_conditions: Dict[int, Tuple[int, int]] = field(default_factory=dict)

    @property
    def crap(self) -> float:
        """CRAP = CC^2 * (1 - coverage)^3 + CC"""
        if self.complexity > 0:
            return (self.complexity ** 2) * ((1 - self.line_rate) ** 3) + self.complexity
        return 0.0

    @property
    def has_tests(self) -> bool:
        return self.lines_covered > 0

    @property
    def tier_thresholds(self) -> dict:
        return TIER_THRESHOLDS[self.tier]

    @property
    def failures(self) -> list[str]:
        reasons = []
        t = self.tier_thresholds
        if not self.has_tests:
            reasons.append("no test coverage")
        if self.line_rate < t["line_coverage"]:
            reasons.append(f"line: {self.line_rate*100:.1f}% (need >= {t['line_coverage']*100:.0f}%)")
        if self.complexity > 0 and self.crap > t["crap"]:
            reasons.append(f"CRAP: {self.crap:.1f} (need <= {t['crap']})")
        if self.branch_rate < t["branch_coverage"]:
            reasons.append(f"branch: {self.branch_rate*100:.1f}% (need >= {t['branch_coverage']*100:.0f}%)")
        return reasons

    @property
    def passes(self) -> bool:
        return len(self.failures) == 0


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def is_generated(filename: str) -> bool:
    return any(pat in filename for pat in GENERATED_PATTERNS)


def normalize_to_forward_slash(raw: str) -> str:
    return raw.replace("\\", "/")


def collapse_ranges(numbers: List[int]) -> str:
    if not numbers:
        return ""
    sorted_nums = sorted(numbers)
    ranges: List[str] = []
    start = prev = sorted_nums[0]
    for n in sorted_nums[1:]:
        if n == prev + 1:
            prev = n
        else:
            ranges.append(f"{start}-{prev}" if start != prev else str(start))
            start = prev = n
    ranges.append(f"{start}-{prev}" if start != prev else str(start))
    return ", ".join(ranges)


def format_line_detail(module: ModuleCoverage) -> List[str]:
    if not module.line_hits:
        return []
    lines: List[str] = []
    uncovered = [lno for lno, hits in module.line_hits.items() if hits == 0]
    if uncovered:
        lines.append(f"        uncovered lines:  {collapse_ranges(uncovered)}")
    partial = sorted(
        lno for lno, (covered, total) in module.branch_conditions.items()
        if covered < total
    )
    if partial:
        parts = [f"{lno} ({module.branch_conditions[lno][0]}/{module.branch_conditions[lno][1]})" for lno in partial]
        lines.append(f"        partial branches: {', '.join(parts)}")
    return lines


def resolve_filename(source_dir: str, raw_filename: str) -> str:
    """Resolve a filename from coverlet XML to a path relative to repo root."""
    abs_path = os.path.normpath(os.path.join(source_dir, raw_filename))
    try:
        rel = os.path.relpath(abs_path, ROOT)
    except ValueError:
        rel = normalize_to_forward_slash(raw_filename)
    return normalize_to_forward_slash(rel)


# ---------------------------------------------------------------------------
# XML parsing
# ---------------------------------------------------------------------------

def _parse_branch_conditions(line_el) -> Optional[Tuple[int, int]]:
    if line_el.get("branch", "").lower() != "true":
        return None
    cond_cov = line_el.get("condition-coverage", "")
    m = CONDITION_COVERAGE_RE.search(cond_cov)
    if m:
        return int(m.group(1)), int(m.group(2))
    return None


def parse_cobertura_xml(xml_path: str) -> List[Tuple[str, str, Dict[int, int], Dict[int, Tuple[int, int]], float]]:
    """Parse a Cobertura XML file.

    Returns list of (resolved_relative_filename, class_name, {line_no: hits},
                      {line_no: (conditions_covered, conditions_total)}, complexity).
    """
    tree = ET.parse(xml_path)
    root = tree.getroot()

    source_dirs = []
    for src_el in root.findall(".//source"):
        if src_el.text:
            source_dirs.append(src_el.text.strip())
    source_dir = source_dirs[0] if source_dirs else ""

    results = []
    for pkg in root.findall(".//package"):
        for cls in pkg.findall(".//class"):
            raw_fname = cls.get("filename", "")
            cname = cls.get("name", "")
            if not raw_fname:
                continue

            resolved = resolve_filename(source_dir, raw_fname)
            complexity = float(cls.get("complexity", 0))

            line_hits: Dict[int, int] = {}
            branch_conds: Dict[int, Tuple[int, int]] = {}
            lines_el = cls.find("lines")
            if lines_el is not None:
                for line_el in lines_el.findall("line"):
                    try:
                        lno = int(line_el.get("number", "0"))
                        hits = int(line_el.get("hits", "0"))
                        line_hits[lno] = max(line_hits.get(lno, 0), hits)
                    except (ValueError, TypeError):
                        continue
                    cond = _parse_branch_conditions(line_el)
                    if cond is not None:
                        branch_conds[lno] = cond

            results.append((resolved, cname, line_hits, branch_conds, complexity))

    return results


# ---------------------------------------------------------------------------
# Test running
# ---------------------------------------------------------------------------

def clean_stale_coverage():
    for xml in sorted(ROOT.glob(XML_PATTERN)):
        xml.unlink()
        print(f"  Cleaned {xml.relative_to(ROOT)}")


def run_tests() -> bool:
    clean_stale_coverage()
    print("\n--- Running tests ---")
    print(f"  {' '.join(TEST_COMMAND)}")
    result = subprocess.run(TEST_COMMAND, cwd=ROOT)
    if result.returncode != 0:
        print(f"  Tests failed (exit code {result.returncode})")
        return False
    return True


# ---------------------------------------------------------------------------
# Coverage collection
# ---------------------------------------------------------------------------

def collect_coverage() -> List[ModuleCoverage]:
    """Parse coverage XMLs, merge line-level data, apply exclusions."""
    xml_files = sorted(ROOT.glob(XML_PATTERN))

    if not xml_files:
        print("[WARN] No coverage XML files found.")
        return []

    print(f"  Found {len(xml_files)} coverage XML files")

    merged: Dict[str, LineLevelData] = {}

    for xml_path in xml_files:
        entries = parse_cobertura_xml(str(xml_path))
        for fname, cname, line_hits, branch_conds, complexity in entries:
            if is_generated(fname):
                continue

            if fname not in merged:
                merged[fname] = LineLevelData(class_name=cname)
            for lno, hits in line_hits.items():
                merged[fname].merge(lno, hits)
            for lno, (covered, total) in branch_conds.items():
                merged[fname].merge_branch(lno, covered, total)
            merged[fname].merge_complexity(complexity)
            if len(cname) > len(merged[fname].class_name):
                merged[fname].class_name = cname

    results: List[ModuleCoverage] = []
    for fname, data in merged.items():
        if data.lines_valid == 0:
            continue

        short_name = data.class_name.split(".")[-1] if data.class_name else ""
        if short_name in EXCLUDED_CLASSES:
            continue

        if data.lines_valid <= DATA_MODEL_MAX_LINES and data.lines_covered == 0:
            continue

        results.append(ModuleCoverage(
            filename=fname,
            class_name=data.class_name,
            line_rate=data.line_rate,
            branch_rate=data.branch_rate,
            lines_valid=data.lines_valid,
            lines_covered=data.lines_covered,
            complexity=data.complexity,
            line_hits=dict(data.lines_hits),
            branch_conditions=dict(data.branch_conditions),
        ))

    return results


# ---------------------------------------------------------------------------
# Tier annotation scanning
# ---------------------------------------------------------------------------

def find_test_file(module: ModuleCoverage) -> Optional[Path]:
    """Find the corresponding test file for a source module."""
    base_name = Path(module.filename).stem
    test_name = f"{base_name}Tests.cs"
    test_dir = ROOT / "DynaDocs.Tests"
    if test_dir.exists():
        matches = list(test_dir.rglob(test_name))
        if matches:
            return matches[0]
    return None


def read_tier_from_test_file(test_file: Path) -> Optional[int]:
    try:
        with open(test_file, "r", encoding="utf-8") as f:
            for i, line in enumerate(f):
                if i >= 10:
                    break
                m = TIER_ANNOTATION_RE.search(line)
                if m:
                    tier = int(m.group(1))
                    if tier in (2, 3):
                        return tier
    except (OSError, UnicodeDecodeError):
        pass
    return None


def assign_tiers(modules: List[ModuleCoverage]) -> None:
    for module in modules:
        test_file = find_test_file(module)
        if test_file is not None:
            tier = read_tier_from_test_file(test_file)
            if tier is not None:
                module.tier = tier


# ---------------------------------------------------------------------------
# Tier registry
# ---------------------------------------------------------------------------

def load_registry() -> dict:
    if TIER_REGISTRY_PATH.exists():
        with open(TIER_REGISTRY_PATH, "r") as f:
            return json.load(f)
    return {}


def save_registry(registry: dict) -> None:
    with open(TIER_REGISTRY_PATH, "w") as f:
        json.dump(registry, f, indent=2, sort_keys=True)
        f.write("\n")


def check_tier_registry(modules: List[ModuleCoverage]) -> List[str]:
    """Sync tier annotations with the registry. Returns list of error messages."""
    registry = load_registry()
    errors = []
    seen_keys = set()

    for module in modules:
        if module.tier > 1:
            key = module.filename
            seen_keys.add(key)
            test_file = find_test_file(module)
            test_path = str(test_file.relative_to(ROOT)) if test_file else ""
            registry[key] = {"tier": module.tier, "test_file": normalize_to_forward_slash(test_path)}

    for key, entry in registry.items():
        if key not in seen_keys:
            errors.append(
                f"TIER REGISTRY: '{key}' is registered as T{entry['tier']} "
                f"but no @test-tier annotation found in test file. "
                f"If demotion is intentional, remove the entry from tier_registry.json manually."
            )

    save_registry(registry)
    return errors


# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------

def print_report(modules: List[ModuleCoverage], *, detail: bool = False) -> bool:
    """Print coverage report. Returns True if any module fails."""
    total = len(modules)
    passing = sum(1 for m in modules if m.passes)
    failing = total - passing
    pct = passing / total * 100 if total else 0

    print("=== COVERAGE GAP CHECK ===\n")
    print(f"  Total modules:  {total}")
    print(f"  Passing:        {passing}  ({pct:.1f}%)")
    print(f"  Failing:        {failing}")

    failing_modules = [m for m in modules if not m.passes]
    if failing_modules:
        print()
        for m in sorted(failing_modules, key=lambda m: m.lines_valid, reverse=True):
            reasons = "  |  ".join(m.failures)
            print(f"  FAIL  {m.filename}  [T{m.tier}]")
            print(f"        {reasons}")
            if detail:
                for line in format_line_detail(m):
                    print(line)

        print(f"\n  [RESULT] {failing} modules fail tier requirements. See details above.")
        return True
    else:
        print(f"\n  [RESULT] All modules pass tier requirements.")
        return False


def print_inspect_report(modules: List[ModuleCoverage], pattern: str) -> None:
    pattern_lower = pattern.lower()
    matched = [
        m for m in modules
        if pattern_lower in m.filename.lower() or pattern_lower in m.class_name.lower()
    ]

    if not matched:
        print(f"\n  No modules matching '{pattern}'.")
        words = pattern_lower.replace("/", " ").replace("\\", " ").replace(".", " ").split()
        suggestions = set()
        for word in words:
            if len(word) < 3:
                continue
            for m in modules:
                if word in m.filename.lower() or word in m.class_name.lower():
                    suggestions.add(m.filename)
        if suggestions:
            print("  Did you mean one of these?")
            for s in sorted(suggestions)[:10]:
                print(f"    - {s}")
        else:
            print("  Available modules (showing first 10):")
            for m in sorted(modules, key=lambda x: x.filename)[:10]:
                print(f"    - {m.filename}")
        return

    print(f"\n=== INSPECT: '{pattern}' ({len(matched)} match{'es' if len(matched) != 1 else ''}) ===\n")

    for m in sorted(matched, key=lambda x: x.filename):
        status = "PASS" if m.passes else "FAIL"
        print(f"  [{status}]  {m.filename}  [T{m.tier}]")
        print(f"        lines: {m.lines_covered}/{m.lines_valid} ({m.line_rate*100:.1f}%)  "
              f"branches: {m.branch_rate*100:.1f}%  "
              f"CRAP: {m.crap:.1f}  CC: {m.complexity:.0f}")
        if m.failures:
            print(f"        failures: {' | '.join(m.failures)}")
        detail_lines = format_line_detail(m)
        if detail_lines:
            for line in detail_lines:
                print(line)
        print()


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Tier compliance checker")
    parser.add_argument(
        "--skip-tests", action="store_true",
        help="Skip test execution, analyze existing data only",
    )
    parser.add_argument(
        "--detail", action="store_true",
        help="Show uncovered line numbers and partial branches for failing modules",
    )
    parser.add_argument(
        "--inspect", metavar="PATTERN",
        help="Show full line-level detail for modules matching PATTERN",
    )
    args = parser.parse_args()

    # 1. Run tests (unless --skip-tests)
    if not args.skip_tests:
        tests_ok = run_tests()
        if not tests_ok:
            print("\nTests failed. Analyzing available coverage data anyway.")

    # 2. Collect coverage data
    print("\nCollecting coverage data...")
    modules = collect_coverage()

    if not modules:
        print("\nNo coverage data found.")
        if args.skip_tests:
            print("Run without --skip-tests to execute tests first.")
        sys.exit(1)

    # 3. Assign tiers from annotations
    assign_tiers(modules)

    # 4. Check tier registry
    registry_errors = check_tier_registry(modules)

    # 5. Print report
    print()
    has_failures = print_report(modules, detail=args.detail)

    # 6. Print inspect report (if requested)
    if args.inspect:
        print_inspect_report(modules, args.inspect)

    # 7. Print registry errors
    if registry_errors:
        print()
        for err in registry_errors:
            print(f"  ERROR: {err}")

    # 8. Exit
    if has_failures or registry_errors:
        sys.exit(1)
    sys.exit(0)


if __name__ == "__main__":
    main()
