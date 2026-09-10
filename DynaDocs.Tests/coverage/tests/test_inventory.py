"""The mutation handoff inventory is exact, complete, and fail-closed."""
import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from inventory import build_file_rows, fingerprint, git_file_state, source_fingerprint, source_path


def _git(root, *arguments):
    return subprocess.run(['git', *arguments], cwd=root, check=True, capture_output=True)


class InventoryTests(unittest.TestCase):
    def test_fingerprint_uses_compact_sorted_json_and_includes_deletions(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "alive.py").write_text("print(1)\n", encoding="utf-8")
            rows = build_file_rows(root, ["alive.py", "gone.py"], deleted={"gone.py"})
            self.assertEqual({"path": "gone.py", "sha256": None, "deleted": True}, rows[1])
            expected = hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(",", ":"))
                                      .encode("utf-8")).hexdigest()
            self.assertEqual(expected, source_fingerprint(rows))

    def test_duplicate_case_and_missing_unaccounted_file_fail(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "a.py").write_text("pass\n", encoding="utf-8")
            with self.assertRaises(ValueError):
                build_file_rows(root, ["a.py", "A.py"])
            with self.assertRaises(ValueError):
                build_file_rows(root, ["missing.py"])


    def test_noncanonical_and_uncased_identities_never_reach_the_filesystem(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "a.py").write_text("pass\n", encoding="utf-8")
            for relative in ["", "../a.py", "sub\\a.py", "C:/a.py", "/a.py"]:
                with self.subTest(relative=relative), self.assertRaises(ValueError):
                    build_file_rows(root, [relative])
            for relative in ["missing.py", "A.py"]:
                with self.subTest(relative=relative), self.assertRaises(ValueError):
                    source_path(root, relative)
            self.assertEqual(root / "a.py", source_path(root, "a.py"))

    def test_fingerprint_covers_content_and_ignores_the_listed_order(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            for name in ("a.py", "b.py"):
                (root / name).write_text("pass\n", encoding="utf-8")
            first = fingerprint(root, ["a.py", "b.py"])
            self.assertEqual(first, fingerprint(root, ["b.py", "a.py"]))
            (root / "b.py").write_text("pass  # changed\n", encoding="utf-8")
            self.assertNotEqual(first, fingerprint(root, ["a.py", "b.py"]))
            with self.assertRaises(ValueError):
                fingerprint(root, ["a.py", "gone.py"])

    def test_git_file_state_separates_tracked_untracked_and_absent_tracked_paths(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            _git(root, "init", "--quiet")
            for name in ("tracked.py", "removed.py", "untracked.py"):
                (root / name).write_text("pass\n", encoding="utf-8")
            (root / ".gitignore").write_text("ignored.py\n", encoding="utf-8")
            (root / "ignored.py").write_text("pass\n", encoding="utf-8")
            _git(root, "add", "tracked.py", "removed.py")
            (root / "removed.py").unlink()

            paths, deleted = git_file_state(root)

            self.assertEqual([".gitignore", "removed.py", "tracked.py", "untracked.py"], paths)
            self.assertEqual({"removed.py"}, deleted)


if __name__ == "__main__":
    unittest.main()
