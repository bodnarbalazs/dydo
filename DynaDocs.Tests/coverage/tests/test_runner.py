"""The isolated runner must not install shell completion in the caller's profile."""
import os
import sys
import unittest
import tempfile
import subprocess
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import run_tests
from run_tests import isolated_environment


class RunnerEnvironmentTests(unittest.TestCase):
    def test_child_uses_unknown_shell_and_parent_environment_is_preserved(self):
        with patch.dict(os.environ, {"SHELL": "/bin/bash", "DYDO_AGENT": "example"}):
            before = dict(os.environ)
            child = isolated_environment()
            self.assertEqual("dydo-test-no-shell", child["SHELL"])
            self.assertNotIn("DYDO_AGENT", child)
            self.assertEqual(before, dict(os.environ))
            child["UNRELATED_CHILD_CHANGE"] = "value"
            self.assertNotIn("UNRELATED_CHILD_CHANGE", os.environ)

    def test_dirty_copy_preserves_spaces_and_untracked_nested_files(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            target = Path(folder) / "worktree"
            (root / "new folder").mkdir(parents=True)
            target.mkdir()
            (root / "new folder/file with spaces.py").write_text("new", encoding="utf-8")
            status = "?? new folder/file with spaces.py\0"
            with patch.object(run_tests, "ROOT", root), patch.object(run_tests, "_git", return_value=(status, 0)):
                run_tests.copy_dirty_files(target)
            self.assertEqual("new", (target / "new folder/file with spaces.py").read_text())

    def test_dirty_copy_rejects_git_failure_and_path_escape(self):
        with tempfile.TemporaryDirectory() as folder:
            target = Path(folder)
            for result in (("", 1), ("?? ../escape.py\0", 0)):
                with patch.object(run_tests, "_git", return_value=result), self.assertRaises(ValueError):
                    run_tests.copy_dirty_files(target)

    def test_rename_copies_new_name_and_removes_old_name(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            target = Path(folder) / "worktree"
            root.mkdir()
            target.mkdir()
            (root / "new.py").write_text("new", encoding="utf-8")
            (target / "old.py").write_text("old", encoding="utf-8")
            status = "R  new.py\0old.py\0"
            with patch.object(run_tests, "ROOT", root), patch.object(run_tests, "_git", return_value=(status, 0)):
                run_tests.copy_dirty_files(target)
            self.assertFalse((target / "old.py").exists())
            self.assertEqual("new", (target / "new.py").read_text())

    def test_real_git_enumerates_nested_untracked_files(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            target = Path(folder) / "worktree"
            subprocess.run(["git", "init", "--quiet", str(root)], check=True)
            (root / "nested/new directory").mkdir(parents=True)
            (root / "nested/new directory/source.py").write_text("actual candidate", encoding="utf-8")
            target.mkdir()
            with patch.object(run_tests, "ROOT", root):
                run_tests.copy_dirty_files(target)
            self.assertEqual("actual candidate", (target / "nested/new directory/source.py").read_text())

    def test_test_command_requires_nonempty_test_selection(self):
        command = run_tests.test_command(["--filter", "FullyQualifiedName~Missing"])
        self.assertEqual("RunConfiguration.TreatNoTestsAsError=true", command[-1])
        self.assertEqual(1, command.count("--"))
        with self.assertRaises(ValueError):
            run_tests.test_command(["--", "RunConfiguration.TreatNoTestsAsError=false"])


if __name__ == "__main__":
    unittest.main()

