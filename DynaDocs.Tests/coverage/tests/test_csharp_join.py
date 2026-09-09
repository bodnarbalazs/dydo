"""AltCover joins use original module identity and physical MethodDef tokens."""
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_join import coverage_methods, join_methods


class CSharpJoinTests(unittest.TestCase):
    def fixture(self, root):
        dll = root / "bin/A.dll"
        dll.parent.mkdir()
        dll.write_bytes(b"original")
        source = root / "A.cs"
        source.write_text("class A { int M(bool b) => b ? 1 : 2; }")
        facts = {
            "assembly_name": "A", "path": "bin/A.dll",
            "sha1": hashlib.sha1(dll.read_bytes()).hexdigest(),
            "documents": {str(source): "A.cs"},
            "methods": [{"token": 100663297, "identity": "System.Int32 A::M(System.Boolean)",
                         "points": [{"path": "A.cs", "origin": "maintained", "line": 1}]}],
        }
        xml = f'''<CoverageSession><Modules><Module hash="{facts['sha1']}">
          <ModulePath>{dll}</ModulePath><ModuleName>A</ModuleName>
          <Files><File uid="1" fullPath="{source}" /></Files><Classes><Class><Methods>
          <Method cyclomaticComplexity="9"><MetadataToken>100663297</MetadataToken>
          <Name>System.Int32 A::M(System.Boolean)</Name>
          <SequencePoints><SequencePoint vc="1" uspid="1" ordinal="0" offset="0" sl="1" sc="1" el="1" ec="10" fileid="1" /></SequencePoints>
          <BranchPoints><BranchPoint vc="0" uspid="7" ordinal="0" offset="2" sl="1" path="0" offsetend="4" fileid="1" /><BranchPoint vc="1" uspid="8" ordinal="1" offset="2" sl="1" path="1" offsetend="8" fileid="1" /></BranchPoints>
          </Method></Methods></Class></Classes></Module></Modules></CoverageSession>'''
        return facts, xml

    def test_exact_token_and_native_branch_rows_are_retained(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            facts, xml = self.fixture(root)
            joined = coverage_methods(xml, root, facts, ["bin/A.dll"])
            row = joined["System.Int32 A::M(System.Boolean)"]
            self.assertEqual({"1": 1}, row["files"]["A.cs"]["Lines"])
            self.assertEqual(2, len(row["files"]["A.cs"]["Branches"]))
            self.assertNotIn("cc", row)

    def test_token_name_hash_alias_and_native_field_mismatches_fail_closed(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            facts, xml = self.fixture(root)
            attacks = [
                ("100663297", "100663298", "token"),
                ("System.Int32 A::M(System.Boolean)", "System.Int32 A::N(System.Boolean)", "signature"),
                (facts["sha1"], "0" * 40, "hash"),
                ('vc="1" uspid="1"', 'vc="-2" uspid="1"', "sequence value"),
                (' offsetend="4"', "", "branch"),
            ]
            for old, new, message in attacks:
                with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                    coverage_methods(xml.replace(old, new, 1), root, facts, ["bin/A.dll"])
            with self.assertRaisesRegex(ValueError, "alias"):
                coverage_methods(xml, root, facts, ["other/A.dll"])

    def test_report_file_rows_resolve_generated_documents_to_classified_identities(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            dll = root / "bin/A.dll"
            dll.parent.mkdir()
            dll.write_bytes(b"original")
            package_url = root.parent / "packages/p/1.0/build/Package.cs"
            generated_url = root / "obj/Gen.cs"
            facts = {"assembly_name": "A", "sha1": hashlib.sha1(dll.read_bytes()).hexdigest(),
                     "documents": {str(package_url): "nuget:p/1.0/build/Package.cs",
                                   str(generated_url): "obj/Gen.cs"},
                     "methods": [
                         {"token": 1, "identity": "A::P()", "points": [
                             {"path": "nuget:p/1.0/build/Package.cs", "origin": "package", "line": 1}]},
                         {"token": 2, "identity": "A::G()", "points": [
                             {"path": "obj/Gen.cs", "origin": "generated", "line": 1}]},
                     ]}
            xml = f'''<CoverageSession><Modules><Module hash="{facts['sha1']}">
              <ModulePath>{dll}</ModulePath><ModuleName>A</ModuleName><Files>
              <File uid="1" fullPath="{package_url}" /><File uid="2" fullPath="{generated_url}" />
              </Files><Classes><Class><Methods>
              <Method><MetadataToken>1</MetadataToken><Name>A::P()</Name><SequencePoints><SequencePoint vc="0" uspid="1" ordinal="0" offset="0" sl="1" sc="1" el="1" ec="2" fileid="1" /></SequencePoints><BranchPoints /></Method>
              <Method><MetadataToken>2</MetadataToken><Name>A::G()</Name><SequencePoints><SequencePoint vc="0" uspid="2" ordinal="0" offset="0" sl="1" sc="1" el="1" ec="2" fileid="2" /></SequencePoints><BranchPoints /></Method>
              </Methods></Class></Classes></Module></Modules></CoverageSession>'''
            joined = coverage_methods(xml, root, facts, ["bin/A.dll"])
            self.assertEqual(["nuget:p/1.0/build/Package.cs"], list(joined["A::P()"]["files"]))
            self.assertEqual(["obj/Gen.cs"], list(joined["A::G()"]["files"]))
            with self.assertRaisesRegex(ValueError, "outside PDB document inventory"):
                coverage_methods(xml.replace(str(package_url), str(root.parent / "unknown.cs")),
                                 root, facts, ["bin/A.dll"])

    def test_join_accounts_generated_only_methods_by_origin(self):
        source = {"files": [{"path": "A.cs", "methods": []}],
                  "generated_files": ["obj/Gen.cs", "nuget:p/1.0/build/Package.cs"],
                  "behavior": {"constructors": [], "fragments": [], "structural_methods": [],
                               "declared_methods": []}}
        assembly = {"methods": [
            {"identity": "A::G()", "points": [{"path": "obj/Gen.cs", "origin": "generated"}]},
            {"identity": "A::P()", "points": [{"path": "nuget:p/1.0/build/Package.cs",
                                                   "origin": "package"}]},
            {"identity": "A::N()", "points": []},
        ]}
        joined = join_methods(Path.cwd(), source, assembly, {})
        reasons = {row["identity"]: row for row in joined["accounting"]}
        self.assertEqual("excluded by origin", reasons["A::G()"]["reason"])
        self.assertEqual(["generated"], reasons["A::G()"]["origins"])
        self.assertEqual(["nuget:p/1.0/build/Package.cs"], reasons["A::P()"]["documents"])
        self.assertEqual("no non-hidden portable-PDB points", reasons["A::N()"]["reason"])

    def test_semantic_synthesized_members_are_audited_without_physical_coverage(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source_path = root / "A.cs"
            source_path.write_text("record A { public int Auto { get; set; } }")
            checksum = hashlib.sha256(source_path.read_bytes()).hexdigest()
            point = {"path": "A.cs", "origin": "maintained", "checksum_algorithm": "SHA256",
                     "checksum": checksum, "line": 1, "column": 0, "end_line": 1, "end_column": 45}
            source = {"files": [{"path": "A.cs", "methods": []}], "generated_files": [],
                      "behavior": {"constructors": [], "fragments": [], "declared_methods": [],
                                   "structural_methods": [
                                       {"key": "A::get_Auto()", "reason": "semantic synthesized auto accessor"},
                                       {"key": "A::.ctor(A)", "reason": "semantic synthesized record copy constructor"},
                                   ]}}
            assembly = {"assembly_name": "A", "methods": [
                {"token": 1, "identity": "System.Int32 A::get_Auto()", "key": "A::get_Auto()", "points": [point]},
                {"token": 2, "identity": "System.Void A::.ctor(A)", "key": "A::.ctor(A)", "points": [point]},
            ]}
            joined = join_methods(root, source, assembly, {})
            self.assertEqual([1, 2], [row["token"] for row in joined["accounting"]])
            self.assertTrue(all(row["reason"] == "semantic synthesized member with no authored executable behavior"
                                for row in joined["accounting"]))
            self.assertEqual({"class": "semantic synthesized member",
                              "reason": "semantic synthesized auto accessor"},
                             joined["accounting"][0]["sourceBehavior"])
            self.assertEqual({"total": 2, "groups": [
                {"reason": "semantic synthesized member with no authored executable behavior", "path": "A.cs", "count": 2}
            ]}, joined["accountingSummary"])

    def test_authored_or_generated_named_members_are_never_excluded_without_source_behavior(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source_path = root / "A.cs"
            source_path.write_text("class A { int Auto { get => 1; } }")
            point = {"path": "A.cs", "origin": "maintained", "checksum_algorithm": "SHA256",
                     "checksum": hashlib.sha256(source_path.read_bytes()).hexdigest(), "line": 1,
                     "column": 0, "end_line": 1, "end_column": 36}
            source = {"files": [{"path": "A.cs", "methods": []}], "generated_files": [],
                      "behavior": {"constructors": [], "fragments": [], "declared_methods": [],
                                   "structural_methods": []}}
            for physical in (
                {"token": 1, "identity": "System.Int32 A::get_Auto()", "key": "A::get_Auto()",
                 "generated": True, "points": [point]},
                {"token": 2, "identity": "System.Void A::.ctor()", "key": "A::.ctor()",
                 "generated": True, "points": [point]},
            ):
                with self.subTest(identity=physical["identity"]), self.assertRaisesRegex(ValueError, "Missing physical method coverage"):
                    join_methods(root, source, {"assembly_name": "A", "methods": [physical]}, {})


if __name__ == "__main__":
    unittest.main()
