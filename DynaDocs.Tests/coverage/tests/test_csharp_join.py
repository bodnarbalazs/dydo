"""AltCover joins use original module identity and physical MethodDef tokens."""
import hashlib
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from csharp_join import coverage_methods


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
            "methods": [{"token": 100663297, "identity": "System.Int32 A::M(System.Boolean)",
                         "points": [{"path": "A.cs", "line": 1}]}],
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
                (' offsetend="4"', "", "branch"),
            ]
            for old, new, message in attacks:
                with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                    coverage_methods(xml.replace(old, new, 1), root, facts, ["bin/A.dll"])
            with self.assertRaisesRegex(ValueError, "alias"):
                coverage_methods(xml, root, facts, ["other/A.dll"])


if __name__ == "__main__":
    unittest.main()
