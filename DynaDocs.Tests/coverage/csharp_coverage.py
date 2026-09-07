"""Ordinary Coverlet campaign helpers with exact report and point identities."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
from pathlib import Path

REPORT_NAMES = ("coverage.json", "coverage.opencover.xml", "coverage.cobertura.xml")


def locate_reports(root):
    """Return exactly one report in every configured native format."""
    root = Path(root)
    reports = []
    for name in REPORT_NAMES:
        matches = sorted(path for path in root.rglob(name) if path.is_file())
        if len(matches) != 1:
            raise ValueError(f"Missing or ambiguous Coverlet report {name}: {len(matches)}")
        reports.append(matches[0])
    return reports


def _branch_key(row):
    required = ("Line", "Offset", "EndOffset", "Path", "Ordinal")
    if not isinstance(row, dict) or any(key not in row for key in (*required, "Hits")):
        raise ValueError("Invalid Coverlet branch point")
    return tuple(row[key] for key in required)


def _merge_method(target, incoming, identity):
    if set(target) != {"Lines", "Branches"} or set(incoming) != {"Lines", "Branches"}:
        raise ValueError(f"Invalid Coverlet method facts: {identity}")
    if set(target["Lines"]) != set(incoming["Lines"]):
        raise ValueError(f"Coverlet line inventory mismatch: {identity}")
    for line, hits in incoming["Lines"].items():
        if type(hits) is not int or hits < 0:
            raise ValueError(f"Invalid Coverlet line hits: {identity}:{line}")
        target["Lines"][line] = max(target["Lines"][line], hits)
    existing = {_branch_key(row): row for row in target["Branches"]}
    offered = {_branch_key(row): row for row in incoming["Branches"]}
    if len(existing) != len(target["Branches"]) or len(offered) != len(incoming["Branches"]):
        raise ValueError(f"Duplicate Coverlet branch identity: {identity}")
    if set(existing) != set(offered):
        raise ValueError(f"Coverlet branch inventory mismatch: {identity}")
    for key, row in offered.items():
        hits = row["Hits"]
        if type(hits) is not int or hits < 0:
            raise ValueError(f"Invalid Coverlet branch hits: {identity}")
        existing[key]["Hits"] = max(existing[key]["Hits"], hits)


def merge_coverlet_json(reports):
    """Union hits only after exact module/source/type/signature point matching."""
    merged = {}
    for report in reports:
        if not isinstance(report, dict):
            raise ValueError("Invalid Coverlet JSON root")
        for module, files in report.items():
            if module not in merged:
                merged[module] = json.loads(json.dumps(files))
                continue
            if set(merged[module]) != set(files):
                raise ValueError(f"Coverlet source inventory mismatch: {module}")
            for source, classes in files.items():
                if set(merged[module][source]) != set(classes):
                    raise ValueError(f"Coverlet type inventory mismatch: {source}")
                for class_name, methods in classes.items():
                    current = merged[module][source][class_name]
                    if set(current) != set(methods):
                        raise ValueError(f"Coverlet method inventory mismatch: {class_name}")
                    for identity, facts in methods.items():
                        _merge_method(current[identity], facts, identity)
    return merged


def snapshot_artifacts(root, paths):
    """Bind copied native artifacts to exact campaign-root relative identities."""
    root = Path(root).resolve()
    rows = []
    for path in sorted({Path(path).resolve() for path in paths}):
        try:
            relative = path.relative_to(root).as_posix()
        except ValueError as error:
            raise ValueError(f"Artifact outside campaign root: {path}") from error
        if not path.is_file():
            raise ValueError(f"Missing campaign artifact: {relative}")
        rows.append({"path": relative, "bytes": path.stat().st_size,
                     "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    return rows


def publish_campaign(root, result_root, output, identity_paths):
    """Publish raw reports and exact assembly/PDB/source hashes before cleanup."""
    root, output = Path(root).resolve(), Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    reports = locate_reports(result_root)
    copied = []
    for report in reports:
        destination = output / report.name
        shutil.copy2(report, destination)
        copied.append(destination)
    rows = snapshot_artifacts(root, [*identity_paths, *reports])
    manifest = {"schema": 1, "artifacts": rows}
    (output / "identities.json").write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return copied


def run_campaign(root, result_root, extra_args=None):
    """Build once, then collect the full ordinary suite and out-of-process CLI hits."""
    root, result_root = Path(root).resolve(), Path(result_root).resolve()
    os.environ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    os.environ["MSBUILDDISABLENODEREUSE"] = "1"
    result_root.mkdir(parents=True, exist_ok=False)
    build = subprocess.run([
        "dotnet", "build", "DynaDocs.sln", "-c", "Debug", "-p:RunAnalyzers=false",
        "-p:NuGetAudit=false", "-p:UseSharedCompilation=false",
    ], cwd=root)
    if build.returncode:
        return build.returncode
    metrics_project = root / "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj"
    metrics_build = subprocess.run([
        "dotnet", "build", str(metrics_project), "-c", "Debug", "-p:RunAnalyzers=false",
        "-p:NuGetAudit=false", "-p:UseSharedCompilation=false",
    ], cwd=root)
    if metrics_build.returncode:
        return metrics_build.returncode
    collector = result_root / "collector"
    collector.mkdir()
    console = result_root / "console"
    console.mkdir()
    test_args = [
        "test", "DynaDocs.sln", "-c", "Debug", "--no-build", "-p:RunAnalyzers=false",
        "-p:UseSharedCompilation=false",
        "--collect:XPlat Code Coverage", "--settings", str(root / "DynaDocs.Tests/coverage/coverage.runsettings"),
        "--results-directory", str(collector), *(extra_args or []), "--",
        "RunConfiguration.TreatNoTestsAsError=true",
    ]
    command = [
        "dotnet", "tool", "run", "coverlet", "--", str(root / "bin/Debug/net10.0/dydo.dll"),
        "--target", "dotnet", "--targetargs", subprocess.list2cmdline(test_args),
        "--format", "json", "--format", "opencover", "--format", "cobertura",
        "--output", str(console / "coverage"),
        "--exclude-by-file", "**/obj/**", "--exclude-by-file", "**/bin/**",
    ]
    tests = subprocess.run(command, cwd=root).returncode

    producer = root / "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll"
    producer_runs = [
        ("producer-project", ["--project", str(metrics_project), "--root", str(root)]),
        ("producer-assembly", ["--assembly", str(producer), "--root", str(root)]),
    ]
    for name, arguments in producer_runs:
        destination = result_root / name
        destination.mkdir()
        row = subprocess.run([
            "dotnet", "tool", "run", "coverlet", "--", str(producer),
            "--target", "dotnet", "--targetargs", subprocess.list2cmdline([str(producer), *arguments]),
            "--format", "json", "--format", "opencover", "--format", "cobertura",
            "--output", str(destination / "coverage"),
            "--exclude-by-file", "**/obj/**", "--exclude-by-file", "**/bin/**",
        ], cwd=root, stdout=subprocess.DEVNULL)
        if row.returncode:
            return row.returncode

    facts = result_root / "facts"
    facts.mkdir()
    fact_requests = [
        ("dydo-source.json", ["--project", str(root / "DynaDocs.csproj"), "--root", str(root)]),
        ("dydo-assembly.json", ["--assembly", str(root / "bin/Debug/net10.0/dydo.dll"), "--root", str(root)]),
        ("producer-source.json", ["--project", str(metrics_project), "--root", str(root)]),
        ("producer-assembly.json", ["--assembly", str(producer), "--root", str(root)]),
    ]
    for name, arguments in fact_requests:
        with (facts / name).open("w", encoding="utf-8") as stream:
            row = subprocess.run(["dotnet", str(producer), *arguments], cwd=root, text=True, stdout=stream)
        if row.returncode:
            return row.returncode

    from csharp_join import coverage_methods, join_methods
    from gate_policy import evaluate_policy
    collector_reports = locate_reports(collector)
    console_reports = locate_reports(console)
    project_reports = locate_reports(result_root / "producer-project")
    assembly_reports = locate_reports(result_root / "producer-assembly")
    dydo_data = merge_coverlet_json([
        json.loads(collector_reports[0].read_text(encoding="utf-8-sig")),
        json.loads(console_reports[0].read_text(encoding="utf-8-sig")),
    ])
    producer_data = merge_coverlet_json([
        json.loads(project_reports[0].read_text(encoding="utf-8-sig")),
        json.loads(assembly_reports[0].read_text(encoding="utf-8-sig")),
    ])
    targets = []
    for assembly_name, data, opencover, source_name, assembly_facts in [
        ("dydo", dydo_data, collector_reports[1], "dydo-source.json", "dydo-assembly.json"),
        ("GateMetrics", producer_data, project_reports[1], "producer-source.json", "producer-assembly.json"),
    ]:
        source = json.loads((facts / source_name).read_text(encoding="utf-8"))
        assembly = json.loads((facts / assembly_facts).read_text(encoding="utf-8"))
        coverage = coverage_methods(data, opencover.read_text(encoding="utf-8-sig"), root, assembly_name)
        targets.append({"assembly": assembly_name, **join_methods(root, source, assembly, coverage)})
    modules = [module for target in targets for module in target["modules"]]
    joined = {"schema": 1, "targets": targets, "modules": modules,
              "findings": evaluate_policy(modules)}
    (result_root / "joined.json").write_text(
        json.dumps(joined, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return tests


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--result-root", required=True)
    parser.add_argument("--extra-json", default="[]")
    args = parser.parse_args()
    extra = json.loads(args.extra_json)
    if not isinstance(extra, list) or any(not isinstance(item, str) for item in extra):
        raise SystemExit(2)
    return run_campaign(args.root, args.result_root, extra)


if __name__ == "__main__":
    raise SystemExit(main())
