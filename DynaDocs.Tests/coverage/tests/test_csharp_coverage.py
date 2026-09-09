"""The C# campaign is one exact AltCover eager full-suite invocation."""
import sys
import tempfile
import unittest
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET
from pathlib import Path
from copy import deepcopy

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_coverage import (ASSEMBLY_PROJECTS, _identity_producer, _same_instrumented_map,
                             _same_artifacts, _same_native_map, _same_restored_map, _template_original_map,
                             _write_commands, altcover_commands, snapshot_artifacts)
from csharp_join import coverage_methods, excluded_physical_tokens, join_methods


class CSharpCoverageTests(unittest.TestCase):
    def test_identity_producer_is_built_outside_instrumented_debug_directories(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            command, producer = _identity_producer(root)
            self.assertEqual("Release", command[command.index("-c") + 1])
            self.assertEqual(root / "DynaDocs.Tests/coverage/metrics/bin/Release/net10.0/GateMetrics.dll",
                             producer)
            prepare, _ = altcover_commands(root, root / "evidence")
            inputs = [Path(item.split("=", 1)[1]) for item in prepare
                      if item.startswith("--inputDirectory=")]
            self.assertFalse(any(producer.is_relative_to(path) for path in inputs))

    def test_completed_command_rows_are_persisted_incrementally(self):
        with tempfile.TemporaryDirectory() as folder:
            output = Path(folder)
            rows = [{"name": "build", "exit": 0}]
            _write_commands(output, rows)
            self.assertEqual(rows, __import__("json").loads((output / "commands.json").read_text()))

    def test_prepare_and_runner_commands_are_exact_and_unfiltered(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            output = root / "evidence/csharp"
            prepare, runner = altcover_commands(root, output)
            rendered = " ".join(prepare)
            self.assertIn("--eager", prepare)
            self.assertIn("--visibleBranches", prepare)
            self.assertNotIn("--showGenerated", prepare)
            self.assertIn("--assemblyFilter=^(?!(dydo|DynaDocs.Tests|GateMetrics)$).*", prepare)
            self.assertEqual(3, sum(arg.startswith("--inputDirectory=") for arg in prepare))
            self.assertEqual("runner", runner[5])
            self.assertIn("--no-build", runner)
            self.assertNotIn("--filter", runner)
            self.assertEqual(3, runner.count("--"))
            self.assertTrue(str(output / "template.opencover.xml") in rendered)

    def test_snapshot_hashes_assembly_pdb_source_and_reports(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name in ("A.dll", "A.pdb", "A.cs", "coverage.opencover.xml"):
                (root / name).write_bytes(name.encode())
            rows = snapshot_artifacts(root, [root / name for name in
                ("A.dll", "A.pdb", "A.cs", "coverage.opencover.xml")])
            self.assertEqual(["A.cs", "A.dll", "A.pdb", "coverage.opencover.xml"],
                             [row["path"] for row in rows])
            self.assertTrue(all(len(row["sha256"]) == 64 for row in rows))

    def test_identity_comparison_includes_document_table_and_point_origin(self):
        self.assertEqual({
            "dydo": "DynaDocs.csproj",
            "DynaDocs.Tests": "DynaDocs.Tests/DynaDocs.Tests.csproj",
            "GateMetrics": "DynaDocs.Tests/coverage/metrics/GateMetrics.csproj",
        }, ASSEMBLY_PROJECTS)
        facts = {"assembly_name": "A", "module_id": "m", "documents": {"C:/A.cs": "A.cs"},
                 "methods": [{"token": 1, "identity": "A::M()", "key": "A::M()",
                              "points": [{"path": "A.cs", "origin": "maintained"}]}]}
        changed_document = {**facts, "documents": {"C:/A.cs": "obj/A.cs"}}
        changed_origin = {**facts, "methods": [{**facts["methods"][0],
                                                 "points": [{"path": "A.cs", "origin": "generated"}]}]}
        self.assertFalse(_same_native_map(facts, changed_document))
        self.assertFalse(_same_native_map(facts, changed_origin))

    def test_template_binds_original_tokens_despite_instrumented_renumbering(self):
        root = Path.cwd()
        original = {"facts": {"assembly_name": "A", "sha1": "ab", "methods": [
            {"token": 1, "identity": "A::First()", "points": []},
            {"token": 2, "identity": "A::Second()", "points": []},
        ]}, "aliases": ["bin/A.dll"], "canonical": "bin/A.dll"}
        template = """<CoverageSession><Modules><Module hash=\"ab\"><ModuleName>A</ModuleName>
            <ModulePath>bin/A.dll</ModulePath><Classes><Class><Methods>
            <Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method>
            <Method><MetadataToken>2</MetadataToken><Name>A::Second()</Name></Method>
            </Methods></Class></Classes></Module></Modules></CoverageSession>"""
        self.assertEqual({"bin/A.dll": {1: "A::First()", 2: "A::Second()"}},
                         _template_original_map(template, root, [original]))
        saved = template.replace("bin/A.dll", str(root / "bin/__Saved/A.dll"))
        self.assertEqual({"bin/A.dll": {1: "A::First()", 2: "A::Second()"}},
                         _template_original_map(saved, root, [original]))
        before = {"assembly_name": "A", "module_id": "m", "pdb_sha256": "p",
                  "documents": {"C:/A.cs": "A.cs"}, "methods": [
                      {"token": 1, "identity": "A::First()", "key": "A::First()",
                       "points": [{"path": "A.cs", "origin": "maintained", "offset": 0,
                                   "checksum_algorithm": "SHA256", "checksum": "hash", "line": 1,
                                   "column": 0, "end_line": 1, "end_column": 2}]},
                  ]}
        instrumented = deepcopy(before)
        instrumented["methods"][0]["token"] = 100
        instrumented["methods"][0]["points"][0]["offset"] = 99
        self.assertTrue(_same_instrumented_map(before, instrumented))
        changed_point = deepcopy(instrumented)
        changed_point["methods"][0]["points"][0]["line"] = 2
        self.assertFalse(_same_instrumented_map(before, changed_point))
        ambiguous = deepcopy(instrumented)
        ambiguous["methods"][0]["points"].append(deepcopy(ambiguous["methods"][0]["points"][0]))
        self.assertFalse(_same_instrumented_map(before, ambiguous))
        ambiguous_documents = deepcopy(instrumented)
        ambiguous_documents["documents"]["C:/Other.cs"] = "A.cs"
        ambiguous_before = deepcopy(before)
        ambiguous_before["documents"]["C:/Other.cs"] = "A.cs"
        self.assertFalse(_same_instrumented_map(ambiguous_before, ambiguous_documents))

    def test_template_mapping_and_restoration_mismatches_fail_closed(self):
        original = {"facts": {"assembly_name": "A", "sha1": "ab", "methods": [
            {"token": 1, "identity": "A::First()", "points": []},
        ]}, "aliases": ["bin/A.dll"], "canonical": "bin/A.dll"}
        for method, message in (("<Method><MetadataToken>2</MetadataToken><Name>A::First()</Name></Method>", "Missing original"),
                                ("<Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method><Method><MetadataToken>1</MetadataToken><Name>A::First()</Name></Method>", "Duplicate template"),
                                ("<Method><MetadataToken>1</MetadataToken><Name>A::Other()</Name></Method>", "Template signature")):
            template = f"<CoverageSession><Modules><Module hash=\"ab\"><ModuleName>A</ModuleName><ModulePath>bin/A.dll</ModulePath><Classes><Class><Methods>{method}</Methods></Class></Classes></Module></Modules></CoverageSession>"
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                    _template_original_map(template, Path.cwd(), [original])
        facts = {"assembly_name": "A", "sha256": "dll", "pdb_sha256": "pdb", "module_id": "m",
                 "documents": {"C:/A.cs": "A.cs"}, "methods": [{"token": 1, "identity": "A::First()",
                 "key": "A::First()", "points": [{"path": "A.cs", "origin": "maintained"}]}]}
        for field, value in (("sha256", "other-dll"), ("pdb_sha256", "other-pdb"),
                             ("module_id", "other-mvid"), ("documents", {"C:/A.cs": "Other.cs"})):
            changed = deepcopy(facts)
            changed[field] = value
            with self.subTest(field=field):
                self.assertFalse(_same_restored_map(facts, changed))
        self.assertFalse(_same_artifacts([{"path": "Source.cs", "bytes": 1, "sha256": "before"}],
                                         [{"path": "Source.cs", "bytes": 1, "sha256": "after"}]))

    def test_campaign_applies_source_behavior_eligibility_before_coverage_completeness(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            dll, source = root / "bin/A.dll", root / "A.cs"
            dll.parent.mkdir()
            dll.write_bytes(b"original")
            source.write_text("void M() { }\nrecord A { public int Auto { get; set; } }\n")
            checksum = hashlib.sha256(source.read_bytes()).hexdigest()
            facts = {"assembly_name": "A", "sha1": hashlib.sha1(dll.read_bytes()).hexdigest(),
                     "documents": {str(source): "A.cs"}, "methods": [
                         {"token": 1, "identity": "System.Void A::M()", "key": "A::M()",
                          "points": [{"path": "A.cs", "origin": "maintained", "line": 1,
                                      "column": 0, "end_line": 1, "end_column": 12,
                                      "checksum_algorithm": "SHA256", "checksum": checksum}]},
                         {"token": 2, "identity": "System.Int32 A::get_Auto()", "key": "A::get_Auto()",
                          "points": [{"path": "A.cs", "origin": "maintained", "line": 2,
                                      "column": 0, "end_line": 2, "end_column": 40,
                                      "checksum_algorithm": "SHA256", "checksum": checksum}]},
                     ]}
            source_facts = {
                "files": [{"path": "A.cs", "methods": [{
                    "id": "A::M()", "line": 1, "column": 0, "end_line": 1, "end_column": 12,
                    "constructor": False, "cognitive": 0, "policy_cc": 1, "parameters": 0,
                }]}],
                "generated_files": [],
                "behavior": {
                    "structural_methods": [{
                        "key": "A::get_Auto()", "reason": "semantic synthesized auto accessor"
                    }],
                    "constructors": [], "fragments": [],
                    "declared_methods": [{
                        "key": "A::M()", "path": "A.cs", "line": 1, "column": 0,
                        "end_line": 1, "end_column": 12,
                    }],
                },
            }
            xml = f'''<CoverageSession><Modules><Module hash="{facts['sha1']}"><ModulePath>{dll}</ModulePath>
              <ModuleName>A</ModuleName><Files><File uid="1" fullPath="{source}" /></Files><Classes><Class><Methods>
              <Method><MetadataToken>1</MetadataToken><Name>System.Void A::M()</Name><SequencePoints>
              <SequencePoint vc="1" uspid="1" ordinal="0" offset="0" sl="1" sc="1" el="1" ec="2" fileid="1" />
              </SequencePoints><BranchPoints /></Method></Methods></Class></Classes></Module></Modules></CoverageSession>'''
            excluded = excluded_physical_tokens(source_facts, facts)
            self.assertEqual({2}, excluded)
            coverage = coverage_methods(xml, root, facts, ["bin/A.dll"], excluded)
            self.assertEqual(["System.Void A::M()"], list(coverage))
            joined = join_methods(root, source_facts, facts, coverage)
            self.assertEqual(1, joined["accountingSummary"]["total"])
            self.assertEqual("semantic synthesized auto accessor",
                             joined["accounting"][0]["sourceBehavior"]["reason"])
            no_point = {"assembly_name": "A", "methods": [
                {"token": 3, "identity": "System.Void GuardContext::.ctor()",
                 "key": "DynaDocs.Commands.GuardCommand/GuardContext::.ctor`0()", "points": []}
            ]}
            self.assertEqual(set(), excluded_physical_tokens({"behavior": {"structural_methods": [
                {"key": "DynaDocs.Commands.GuardCommand/GuardContext::.ctor`0()",
                 "reason": "semantic implicit constructor with no authored executable fragments"}
            ]}}, no_point))

    def test_hop3_05_replay_applies_physical_eligibility_to_retained_collector_output(self):
        """Replay only: retained source facts are absent, so this deliberately stops before join."""
        root = Path(__file__).resolve().parents[3]
        raw = root / "DynaDocs.Tests/coverage/results/native-g-20260909-hop3-05/raw"
        self.assertFalse((raw / "source-facts.json").exists())
        pre = json.loads((raw / "identity-pre.json").read_text(encoding="utf-8"))
        assembly = next(row["facts"] for row in pre if row["facts"]["assembly_name"] == "dydo")
        producer = _identity_producer(root)[1]
        process = subprocess.run(["dotnet", str(producer), "--project", str(root / "DynaDocs.csproj"),
                                  "--root", str(root)], text=True, capture_output=True, encoding="utf-8")
        self.assertEqual(0, process.returncode, process.stderr)
        source = json.loads(process.stdout)
        xml = (raw / "coverage.opencover.xml").read_text(encoding="utf-8-sig")
        original_path = next(module for module in ET.fromstring(xml).findall("./Modules/Module")
                             if module.findtext("ModuleName") == "dydo").findtext("ModulePath")
        xml = xml.replace(original_path, str(root / assembly["path"]), 1)
        coverage = coverage_methods(xml, root, assembly, [assembly["path"]],
                                    excluded_physical_tokens(source, assembly))
        self.assertTrue(coverage)

if __name__ == "__main__":
    unittest.main()
