"""Coverage report generator.

Collects Cobertura XML coverage data, generates a unified HTML report
via ReportGenerator, prints a CRAP/T1 summary, and opens the report
in the browser.

Usage:
    python DynaDocs.Tests/coverage/report.py                  # report only
    python DynaDocs.Tests/coverage/report.py --run-tests      # run tests first
    python DynaDocs.Tests/coverage/report.py --no-open        # skip opening browser
"""

import argparse
import re
import shutil
import subprocess
import sys
import webbrowser
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
COVERAGE_DIR = ROOT / "DynaDocs.Tests" / "coverage"
REPORT_DIR = COVERAGE_DIR / "report"
RUNSETTINGS = COVERAGE_DIR / "coverage.runsettings"

XML_PATTERN = "DynaDocs.Tests/**/coverage.cobertura.xml"

TEST_COMMAND = [
    "dotnet", "test", "DynaDocs.sln",
    "--collect:XPlat Code Coverage",
    f"--settings:{RUNSETTINGS}",
]

GENERATED_MARKERS = ["\\obj\\", "/obj/", ".g.cs", ".generated.cs"]

# Async state machine pattern: /<MethodName>d__N or /<<Method>b__N>d
ASYNC_STATE_MACHINE = re.compile(r"/<?<?\w+>?[bdk]__\d+>?d?$")


def find_xmls() -> list[Path]:
    found = sorted(ROOT.glob(XML_PATTERN))
    if not found:
        print(f"  No coverage XML found ({XML_PATTERN})")
    return found


def clean_stale_coverage():
    for xml in sorted(ROOT.glob(XML_PATTERN)):
        xml.unlink()
        print(f"  Cleaned {xml.relative_to(ROOT)}")


def clean_report_dir():
    if REPORT_DIR.exists():
        shutil.rmtree(REPORT_DIR)
    REPORT_DIR.mkdir(parents=True, exist_ok=True)


def run_tests() -> bool:
    clean_stale_coverage()
    print("\n--- Running tests ---")
    print(f"  {' '.join(TEST_COMMAND)}")
    result = subprocess.run(TEST_COMMAND, cwd=ROOT)
    if result.returncode != 0:
        print(f"  Tests failed (exit code {result.returncode})")
        return False
    return True


def generate_report(xmls: list[Path]) -> bool:
    reports_arg = ";".join(str(x) for x in xmls)
    cmd = [
        "reportgenerator",
        f"-reports:{reports_arg}",
        f"-targetdir:{REPORT_DIR}",
        "-reporttypes:Html",
    ]
    print("\n--- Generating report ---")
    result = subprocess.run(cmd, cwd=ROOT, shell=True)
    return result.returncode == 0


# --- CRAP Analysis ---


def is_generated(filename: str, classname: str) -> bool:
    for marker in GENERATED_MARKERS:
        if marker in filename:
            return True
    if "Generated" in classname and ".<" in classname:
        return True
    return False


def normalize_class(name: str) -> str:
    return ASYNC_STATE_MACHINE.sub("", name)


def normalize_filename(filename: str) -> str:
    f = filename.replace("\\", "/")
    for prefix in ["DynaDocs/"]:
        idx = f.find(prefix)
        if idx != -1:
            f = f[idx:]
            break
    return f


def collect_from_xml(xml_file: Path):
    tree = ET.parse(xml_file)
    root = tree.getroot()
    entries = []
    skipped = 0
    for package in root.findall(".//package"):
        for cls in package.findall(".//class"):
            name = cls.get("name", "")
            filename = cls.get("filename", "")
            if is_generated(filename, name):
                skipped += 1
                continue
            line_rate = float(cls.get("line-rate", 0))
            complexity = float(cls.get("complexity", 0))
            if complexity > 0:
                crap = (complexity ** 2) * ((1 - line_rate) ** 3) + complexity
            else:
                crap = 0
            norm_name = normalize_class(name)
            norm_file = normalize_filename(filename)
            entries.append((norm_name, norm_file, crap, complexity, line_rate))
    return entries, skipped


def crap_summary(xmls: list[Path]):
    all_entries = []
    total_skipped = 0

    for xml_file in xmls:
        entries, skipped = collect_from_xml(xml_file)
        all_entries.extend(entries)
        total_skipped += skipped

    groups: dict[tuple, dict] = {}
    for norm_name, norm_file, crap, cc, cov in all_entries:
        key = (norm_name, norm_file)
        if key not in groups:
            groups[key] = {"total_cc": 0, "max_crap": 0, "min_cov": 1.0,
                           "name": norm_name, "file": norm_file}
        g = groups[key]
        g["total_cc"] = max(g["total_cc"], cc)
        g["max_crap"] = max(g["max_crap"], crap)
        g["min_cov"] = min(g["min_cov"], cov)

    results = []
    for g in groups.values():
        results.append((g["max_crap"], g["total_cc"], g["min_cov"],
                        g["name"], g["file"]))
    results.sort(reverse=True)

    total = len(results)
    failing = [r for r in results if r[0] > 30]
    passing = total - len(failing)
    pct = passing / total * 100 if total else 0

    print(f"\n=== T1 Summary ===")
    print(f"Total unique source classes: {total} (skipped {total_skipped} generated)")
    print(f"  Pass: {passing}/{total} ({pct:.0f}%)")
    print(f"  Fail: {len(failing)}/{total}")

    if failing:
        print(f"\nT1 failures (CRAP > 30):")
        print(f"{'CRAP':>8} {'CC':>5} {'Cov%':>6} Class (file)")
        print("-" * 100)
        for crap, cc, cov, name, filename in failing:
            print(f"{crap:8.1f} {cc:5.0f} {cov*100:5.1f}% {name} ({filename})")
    else:
        print(f"\nAll {total} classes pass T1!")


def main():
    parser = argparse.ArgumentParser(description="Coverage report generator")
    parser.add_argument(
        "--run-tests", action="store_true",
        help="Run tests with coverage collection before generating report",
    )
    parser.add_argument(
        "--no-open", action="store_true",
        help="Don't open the report in a browser",
    )
    args = parser.parse_args()

    if args.run_tests:
        if not run_tests():
            print("\nTests failed. Generating report from available data anyway.")

    xmls = find_xmls()
    if not xmls:
        print("\nNo coverage data found. Run with --run-tests to collect coverage first.")
        sys.exit(1)

    print(f"\nFound {len(xmls)} coverage file(s):")
    for x in xmls:
        print(f"  {x.relative_to(ROOT)}")

    clean_report_dir()

    if not generate_report(xmls):
        print("\nReport generation failed.")
        sys.exit(1)

    crap_summary(xmls)

    report_index = REPORT_DIR / "index.html"
    print(f"\nReport: {report_index}")

    if not args.no_open:
        webbrowser.open(report_index.as_uri())


if __name__ == "__main__":
    main()
