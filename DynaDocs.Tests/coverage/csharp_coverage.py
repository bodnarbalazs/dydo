"""One isolated AltCover 9.0.102 eager campaign with exact native identities."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import time
from pathlib import Path


ASSEMBLY_NAMES = {"dydo", "DynaDocs.Tests", "GateMetrics"}
ASSEMBLY_PROJECTS = {
    "dydo": "DynaDocs.csproj",
    "DynaDocs.Tests": "DynaDocs.Tests/DynaDocs.Tests.csproj",
    "GateMetrics": "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj",
}


def snapshot_artifacts(root, paths):
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


def altcover_commands(root, output):
    root, output = Path(root).resolve(), Path(output).resolve()
    inputs = [root / "bin/Debug/net10.0", root / "DynaDocs.Tests/bin/Debug/net10.0",
              root / "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0"]
    prepare = ["dotnet", "tool", "run", "altcover", "--",
               *(f"--inputDirectory={path}" for path in inputs), "--inplace",
               f"--report={output / 'template.opencover.xml'}", "--reportFormat=OpenCover",
               "--eager", "--localSource", "--visibleBranches",
               "--assemblyFilter=^(?!(dydo|DynaDocs.Tests|GateMetrics)$).*"]
    runner = ["dotnet", "tool", "run", "altcover", "--", "runner",
              f"--recorderDirectory={root / 'DynaDocs.Tests/bin/Debug/net10.0'}",
              f"--workingDirectory={root}", "--executable=dotnet",
              f"--outputFile={output / 'coverage.opencover.xml'}", "--summary=N", "--",
              "test", "DynaDocs.sln", "-c", "Debug", "--no-build", "-p:RunAnalyzers=false",
              "-p:UseSharedCompilation=false", "--", "RunConfiguration.TreatNoTestsAsError=true"]
    return prepare, runner


def _run(name, command, root, output):
    prefix = output / name
    started = time.monotonic()
    result = subprocess.run(list(map(str, command)), cwd=root, text=True, encoding="utf-8",
                            errors="replace", capture_output=True)
    stdout, stderr = Path(str(prefix) + ".stdout"), Path(str(prefix) + ".stderr")
    stdout.write_text(result.stdout, encoding="utf-8")
    stderr.write_text(result.stderr, encoding="utf-8")
    return {"name": name, "argv": list(map(str, command)), "cwd": str(root),
            "exit": result.returncode, "elapsedSeconds": round(time.monotonic() - started, 6),
            "stdout": stdout.name, "stdoutSha256": hashlib.sha256(stdout.read_bytes()).hexdigest(),
            "stderr": stderr.name, "stderrSha256": hashlib.sha256(stderr.read_bytes()).hexdigest()}


def _assembly_facts(root, producer, assembly):
    project = root / ASSEMBLY_PROJECTS[assembly.stem]
    row = subprocess.run(["dotnet", str(producer), "--assembly", str(assembly), "--root", str(root),
                          "--project", str(project)],
                         cwd=root, text=True, encoding="utf-8", errors="strict", capture_output=True)
    if row.returncode != 0:
        raise ValueError(f"Assembly identity failed for {assembly}: {row.stderr.strip()}")
    facts = json.loads(row.stdout)
    facts["sha1"] = hashlib.sha1(assembly.read_bytes()).hexdigest()
    return facts


def _equivalence_key(facts):
    identity = {"documents": facts["documents"], "methods": [
        {"token": row["token"], "identity": row["identity"], "points": row["points"]}
        for row in facts["methods"]]}
    method_hash = hashlib.sha256(json.dumps(identity, sort_keys=True, separators=(",", ":"))
                                 .encode("utf-8")).hexdigest()
    return (facts["assembly_name"], facts["sha256"], facts["pdb_sha256"], facts["module_id"], method_hash)


def _identity_classes(root, producer, paths):
    classes = {}
    for path in paths:
        facts = _assembly_facts(root, producer, path)
        key = _equivalence_key(facts)
        if any(existing[0] == key[0] and existing != key for existing in classes):
            raise ValueError(f"Conflicting same-name assembly identity: {key[0]}")
        row = classes.setdefault(key, {"facts": facts, "aliases": []})
        row["aliases"].append(path.resolve().relative_to(root).as_posix())
    result = []
    for row in classes.values():
        row["aliases"].sort()
        row["canonical"] = row["aliases"][0]
        result.append(row)
    return sorted(result, key=lambda row: row["canonical"])


def _candidate_assemblies(root):
    directories = [root / "bin/Debug/net10.0", root / "DynaDocs.Tests/bin/Debug/net10.0",
                   root / "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0"]
    paths = [path for directory in directories for path in directory.glob("*.dll")
             if path.stem in ASSEMBLY_NAMES and path.with_suffix(".pdb").is_file()]
    if {path.stem for path in paths} != ASSEMBLY_NAMES:
        raise ValueError("Missing exact C# campaign assemblies")
    return sorted(set(paths))


def _same_native_map(before, after):
    keys = ("assembly_name", "module_id")
    return (all(before[key] == after[key] for key in keys)
            and before["documents"] == after["documents"] and [
        (row["token"], row["identity"], row["key"], row["points"]) for row in before["methods"]
    ] == [(row["token"], row["identity"], row["key"], row["points"]) for row in after["methods"]])


def run_campaign(root, result_root, extra_args=None):
    root, result_root = Path(root).resolve(), Path(result_root).resolve()
    if extra_args:
        raise ValueError("Assurance coverage requires the ordinary unfiltered full suite")
    result_root.mkdir(parents=True, exist_ok=False)
    os.environ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    os.environ["MSBUILDDISABLENODEREUSE"] = "1"
    commands = []
    builds = [
        ["dotnet", "build", "DynaDocs.sln", "-c", "Debug", "-p:RunAnalyzers=false",
         "-p:NuGetAudit=false", "-p:UseSharedCompilation=false"],
        ["dotnet", "build", "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj", "-c", "Debug",
         "-p:RunAnalyzers=false", "-p:NuGetAudit=false", "-p:UseSharedCompilation=false"],
    ]
    for index, command in enumerate(builds):
        row = _run(f"build-{index}", command, root, result_root)
        commands.append(row)
        if row["exit"]:
            (result_root / "commands.json").write_text(json.dumps(commands, indent=2) + "\n")
            return 2
    producer = root / "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll"
    assembly_paths = _candidate_assemblies(root)
    pre = _identity_classes(root, producer, assembly_paths)
    originals = result_root / "originals"
    for path in assembly_paths:
        relative = path.relative_to(root)
        destination = originals / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
        shutil.copy2(path.with_suffix(".pdb"), destination.with_suffix(".pdb"))
    (result_root / "identity-pre.json").write_text(json.dumps(pre, indent=2, sort_keys=True) + "\n")
    prepare, runner = altcover_commands(root, result_root)
    row = _run("altcover-prepare", prepare, root, result_root)
    commands.append(row)
    if row["exit"]:
        (result_root / "commands.json").write_text(json.dumps(commands, indent=2) + "\n")
        return 2
    instrumented = _identity_classes(root, producer, assembly_paths)
    before_by_alias = {alias: row["facts"] for row in pre for alias in row["aliases"]}
    for row in instrumented:
        for alias in row["aliases"]:
            if alias not in before_by_alias or not _same_native_map(before_by_alias[alias], row["facts"]):
                raise ValueError(f"AltCover changed MethodDef/PDB identity: {alias}")
    (result_root / "identity-instrumented.json").write_text(
        json.dumps(instrumented, indent=2, sort_keys=True) + "\n")
    row = _run("altcover-runner", runner, root, result_root)
    commands.append(row)
    (result_root / "commands.json").write_text(json.dumps(commands, indent=2, sort_keys=True) + "\n")
    if row["exit"]:
        return row["exit"]
    report = result_root / "coverage.opencover.xml"
    if not report.is_file():
        raise ValueError("AltCover runner produced no collected OpenCover report")
    post = _identity_classes(root, producer, assembly_paths)
    for row in post:
        for alias in row["aliases"]:
            if alias not in {item for before in instrumented for item in before["aliases"]}:
                raise ValueError(f"Post-campaign assembly alias changed: {alias}")
    (result_root / "identity-post.json").write_text(json.dumps(post, indent=2, sort_keys=True) + "\n")
    from csharp_join import coverage_methods, join_methods
    from gate_policy import evaluate_policy
    xml = report.read_text(encoding="utf-8-sig")
    normalized = {}
    for equivalence in pre:
        normalized[equivalence["facts"]["assembly_name"]] = coverage_methods(
            xml, root, equivalence["facts"], equivalence["aliases"])
    targets = []
    projects = [("dydo", root / "DynaDocs.csproj"),
                ("GateMetrics", root / "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj")]
    for name, project in projects:
        source_row = subprocess.run(["dotnet", str(producer), "--project", str(project), "--root", str(root)],
                                    cwd=root, text=True, encoding="utf-8", capture_output=True)
        if source_row.returncode:
            raise ValueError(f"Source identity failed for {name}: {source_row.stderr.strip()}")
        source = json.loads(source_row.stdout)
        assembly = next(row["facts"] for row in pre if row["facts"]["assembly_name"] == name)
        targets.append({"assembly": name, **join_methods(root, source, assembly, normalized[name])})
    modules = [module for target in targets for module in target["modules"]]
    joined = {"schema": 1, "targets": targets, "modules": modules,
              "findings": evaluate_policy(modules)}
    (result_root / "joined.json").write_text(json.dumps(joined, indent=2, sort_keys=True) + "\n")
    return 0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--result-root", required=True)
    parser.add_argument("--extra-json", default="[]")
    args = parser.parse_args()
    try:
        extra = json.loads(args.extra_json)
        if not isinstance(extra, list) or any(not isinstance(item, str) for item in extra):
            return 2
        return run_campaign(args.root, args.result_root, extra)
    except (ValueError, KeyError, OSError, json.JSONDecodeError) as error:
        print(error, file=os.sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
