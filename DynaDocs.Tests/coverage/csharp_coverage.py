"""One isolated AltCover 9.0.102 eager campaign with exact native identities."""
import argparse
import hashlib
import json
import os
import shutil
import subprocess
import time
import xml.etree.ElementTree as ET
from collections import Counter
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


def _write_commands(output, commands):
    (Path(output) / "commands.json").write_text(
        json.dumps(commands, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def _identity_producer(root):
    root = Path(root).resolve()
    project = "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj"
    command = ["dotnet", "build", project, "-c", "Release", "-p:RunAnalyzers=false",
               "-p:NuGetAudit=false", "-p:UseSharedCompilation=false"]
    producer = root / "DynaDocs.Tests/coverage/metrics/bin/Release/net10.0/GateMetrics.dll"
    return command, producer


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


def _same_instrumented_map(before, after):
    fields = ("path", "origin", "checksum_algorithm", "checksum", "line", "column",
              "end_line", "end_column")
    def points(facts):
        values = []
        for method in facts["methods"]:
            for point in method["points"]:
                if any(field not in point for field in fields):
                    return None
                values.append(tuple(point[field] for field in fields))
        return Counter(values)
    before_points, after_points = points(before), points(after)
    documents = before["documents"]
    return (before["assembly_name"] == after["assembly_name"]
            and before["module_id"] == after["module_id"]
            and before["pdb_sha256"] == after["pdb_sha256"]
            and documents == after["documents"]
            and len(documents) == len(set(documents.values()))
            and before_points is not None and before_points == after_points)


def _same_restored_map(before, after):
    return before == after


def _same_artifacts(before, after):
    return before == after


def _campaign_identity_paths(root, assembly_paths, identities):
    paths = [path for assembly in assembly_paths for path in (assembly, assembly.with_suffix(".pdb"))]
    sources = {point["path"] for row in identities for method in row["facts"]["methods"]
               for point in method["points"] if point["origin"] == "maintained"}
    return [*paths, *(Path(root) / source for source in sorted(sources))]


def _altcover_aliases(aliases):
    aliases = sorted(set(aliases))
    saved = [(Path(alias).parent / "__Saved" / Path(alias).name).as_posix()
             for alias in aliases]
    return sorted(set([*aliases, *saved]))


def _template_original_map(opencover, root, originals):
    """Bind each template token/signature pair to one staged original method."""
    root = Path(root).resolve()
    modules = ET.fromstring(opencover).findall("./Modules/Module")
    result = {}
    for original in originals:
        facts, aliases = original["facts"], sorted(set(original["aliases"]))
        matching = [module for module in modules
                    if module.findtext("ModuleName") == facts["assembly_name"]]
        if len(matching) != 1:
            raise ValueError(f"Missing or ambiguous template module: {facts['assembly_name']}")
        module = matching[0]
        module_path = Path(module.findtext("ModulePath"))
        module_path = module_path if module_path.is_absolute() else root / module_path
        try:
            report_alias = module_path.resolve().relative_to(root).as_posix()
        except ValueError as error:
            raise ValueError(f"Template module outside campaign root: {module_path}") from error
        if report_alias not in _altcover_aliases(aliases):
            raise ValueError(f"Unknown template module alias: {module_path}")
        module_hash = module.attrib.get("hash", "").replace("-", "").lower()
        if module_hash != facts["sha1"].lower():
            raise ValueError("Original template module hash mismatch")
        methods = {}
        for row in facts["methods"]:
            methods.setdefault(row["token"], []).append(row["identity"])
        mapped = {}
        for method in module.findall("./Classes/Class/Methods/Method"):
            token_text, name = method.findtext("MetadataToken"), method.findtext("Name")
            if not token_text or not token_text.isdecimal():
                raise ValueError("Missing template MethodDef token")
            token = int(token_text)
            if token in mapped:
                raise ValueError(f"Duplicate template MethodDef token: {token}")
            matches = methods.get(token, [])
            if not matches:
                raise ValueError(f"Missing original MethodDef token: {token}")
            if len(matches) != 1:
                raise ValueError(f"Duplicate original MethodDef token: {token}")
            if name != matches[0]:
                raise ValueError(f"Template signature mismatch for token {token}")
            mapped[token] = name
        result[original["canonical"]] = mapped
    if len(modules) != len(result):
        raise ValueError("Template module missing original identity")
    return result


def _source_facts(root, producer, name):
    project = root / ASSEMBLY_PROJECTS[name]
    row = subprocess.run(["dotnet", str(producer), "--project", str(project), "--root", str(root)],
                         cwd=root, text=True, encoding="utf-8", capture_output=True)
    if row.returncode:
        raise ValueError(f"Source identity failed for {name}: {row.stderr.strip()}")
    return json.loads(row.stdout)


def run_campaign(root, result_root, extra_args=None):
    root, result_root = Path(root).resolve(), Path(result_root).resolve()
    if extra_args:
        raise ValueError("Assurance coverage requires the ordinary unfiltered full suite")
    result_root.mkdir(parents=True, exist_ok=False)
    os.environ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"
    os.environ["MSBUILDDISABLENODEREUSE"] = "1"
    commands = []
    producer_build, producer = _identity_producer(root)
    builds = [
        ["dotnet", "build", "DynaDocs.sln", "-c", "Debug", "-p:RunAnalyzers=false",
         "-p:NuGetAudit=false", "-p:UseSharedCompilation=false"],
        ["dotnet", "build", "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj", "-c", "Debug",
         "-p:RunAnalyzers=false", "-p:NuGetAudit=false", "-p:UseSharedCompilation=false"],
        producer_build,
    ]
    for index, command in enumerate(builds):
        row = _run(f"build-{index}", command, root, result_root)
        commands.append(row)
        _write_commands(result_root, commands)
        if row["exit"]:
            return 2
    producer_inputs = [path for path in producer.parent.iterdir() if path.is_file()]
    (result_root / "identity-producer.json").write_text(json.dumps({
        "schema": 1,
        "artifacts": snapshot_artifacts(root, producer_inputs),
    }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
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
    pre_artifacts = snapshot_artifacts(root, _campaign_identity_paths(root, assembly_paths, pre))
    (result_root / "identity-pre-artifacts.json").write_text(
        json.dumps(pre_artifacts, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    prepare, runner = altcover_commands(root, result_root)
    row = _run("altcover-prepare", prepare, root, result_root)
    commands.append(row)
    _write_commands(result_root, commands)
    if row["exit"]:
        return 2
    template = result_root / "template.opencover.xml"
    if not template.is_file():
        raise ValueError("AltCover prepare produced no template OpenCover report")
    template_map = _template_original_map(template.read_text(encoding="utf-8-sig"), root, pre)
    (result_root / "template-original-map.json").write_text(
        json.dumps(template_map, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    instrumented = _identity_classes(root, producer, assembly_paths)
    (result_root / "identity-instrumented.json").write_text(
        json.dumps(instrumented, indent=2, sort_keys=True) + "\n")
    before_by_alias = {alias: row["facts"] for row in pre for alias in row["aliases"]}
    for row in instrumented:
        for alias in row["aliases"]:
            if alias not in before_by_alias or not _same_instrumented_map(before_by_alias[alias], row["facts"]):
                raise ValueError(f"AltCover changed portable-PDB identity: {alias}")
    instrumented_artifacts = snapshot_artifacts(root, _campaign_identity_paths(root, assembly_paths, instrumented))
    stable_paths = {row["path"] for row in pre_artifacts if not row["path"].endswith(".dll")}
    if ([row for row in pre_artifacts if row["path"] in stable_paths]
            != [row for row in instrumented_artifacts if row["path"] in stable_paths]):
        raise ValueError("AltCover changed portable-PDB or source bytes")
    row = _run("altcover-runner", runner, root, result_root)
    commands.append(row)
    _write_commands(result_root, commands)
    if row["exit"]:
        return row["exit"]
    report = result_root / "coverage.opencover.xml"
    if not report.is_file():
        raise ValueError("AltCover runner produced no collected OpenCover report")
    collected_map = _template_original_map(report.read_text(encoding="utf-8-sig"), root, pre)
    if collected_map != template_map:
        raise ValueError("Collected report changed template MethodDef namespace")
    for path in assembly_paths:
        staged = originals / path.relative_to(root)
        shutil.copy2(staged, path)
        shutil.copy2(staged.with_suffix(".pdb"), path.with_suffix(".pdb"))
    post = _identity_classes(root, producer, assembly_paths)
    post_artifacts = snapshot_artifacts(root, _campaign_identity_paths(root, assembly_paths, post))
    if not _same_artifacts(pre_artifacts, post_artifacts):
        raise ValueError("Post-campaign artifact restoration mismatch")
    before_by_alias = {alias: row["facts"] for row in pre for alias in row["aliases"]}
    for row in post:
        for alias in row["aliases"]:
            if alias not in before_by_alias or not _same_restored_map(before_by_alias[alias], row["facts"]):
                raise ValueError(f"Post-campaign restoration mismatch: {alias}")
    (result_root / "identity-post.json").write_text(json.dumps(post, indent=2, sort_keys=True) + "\n")
    from csharp_join import coverage_methods, excluded_physical_tokens, join_methods
    from gate_policy import evaluate_policy
    xml = report.read_text(encoding="utf-8-sig")
    sources = {name: _source_facts(root, producer, name) for name in ASSEMBLY_PROJECTS}
    normalized = {}
    for equivalence in pre:
        name = equivalence["facts"]["assembly_name"]
        normalized[equivalence["facts"]["assembly_name"]] = coverage_methods(
            xml, root, equivalence["facts"], _altcover_aliases(equivalence["aliases"]),
            excluded_physical_tokens(sources[name], equivalence["facts"]))
    targets = []
    for name in ("dydo", "GateMetrics"):
        assembly = next(row["facts"] for row in pre if row["facts"]["assembly_name"] == name)
        targets.append({"assembly": name, **join_methods(root, sources[name], assembly, normalized[name])})
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
