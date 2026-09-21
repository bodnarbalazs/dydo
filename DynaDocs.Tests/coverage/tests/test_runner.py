"""The isolated runner must not install shell completion in the caller's profile."""
import hashlib
import json
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

CAMPAIGN_BINARIES = ["bin/Debug/net10.0/dydo.dll", "bin/Debug/net10.0/dydo.pdb",
                     "DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.dll",
                     "DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.pdb",
                     "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll",
                     "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.pdb"]
CAMPAIGN_RESULTS = "dydo/_system/.local/csharp-campaign-results"
UNMEASURED = ["obj/Debug/Generated.cs", "bin/Debug/net10.0/Skipped.cs",
              "node_modules/pkg/Vendor.cs", ".git/Hook.cs", ".local/Local.cs"]


def write_file(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


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


def _make_link(link, target):
    """Create a link matching what setup-skills.mjs produces on this platform, for test fixtures."""
    link.parent.mkdir(parents=True, exist_ok=True)
    if sys.platform == "win32":
        import _winapi
        _winapi.CreateJunction(str(target), str(link))
    else:
        os.symlink(target, link, target_is_directory=True)


class SkillLinkMaterializationTests(unittest.TestCase):
    def test_real_directory_under_host_root_is_reproduced_as_real_directory(self):
        # A host-authored copy (a DR 049 violation) must survive into the snapshot as a real
        # directory, not vanish or become a link -- otherwise the guard can never see it and fails
        # vacuously under the isolated runner.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            worktree = Path(folder) / "worktree"
            write_file(root / ".claude/skills/rogue/SKILL.md", "authored copy")
            write_file(worktree / "skills/admiral/SKILL.md", "canonical")

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            rogue = worktree / ".claude/skills/rogue"
            self.assertTrue(rogue.is_dir())
            self.assertFalse(rogue.is_symlink())
            self.assertEqual("authored copy", (rogue / "SKILL.md").read_text())

    def test_link_entry_is_rebased_to_resolve_inside_snapshot_skills(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            worktree = Path(folder) / "worktree"
            write_file(root / "skills/admiral/SKILL.md", "canonical")
            write_file(worktree / "skills/admiral/SKILL.md", "canonical")
            _make_link(root / ".claude/skills/admiral", root / "skills/admiral")

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            link = worktree / ".claude/skills/admiral"
            resolved = Path(os.path.realpath(link))
            self.assertEqual(Path(os.path.realpath(worktree / "skills/admiral")), resolved)

    def test_foreign_link_target_outside_skills_is_reproduced_unchanged(self):
        # A link that does not resolve inside skills/ is itself the DR 049 violation; it must still
        # resolve outside <snapshot>/skills so the guard fails there exactly as it would on the host.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            worktree = Path(folder) / "worktree"
            outside = Path(folder) / "outside-target"
            outside.mkdir(parents=True)
            write_file(worktree / "skills/admiral/SKILL.md", "canonical")
            _make_link(root / ".claude/skills/rogue-link", outside)

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            link = worktree / ".claude/skills/rogue-link"
            self.assertEqual(Path(os.path.realpath(outside)), Path(os.path.realpath(link)))

    def test_absent_source_root_projects_one_link_per_canonical_snapshot_skill(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            write_file(worktree / "skills/admiral/SKILL.md", "canonical")
            write_file(worktree / "skills/bro/SKILL.md", "canonical")

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            for relative in (".claude/skills", ".agents/skills"):
                for name in ("admiral", "bro"):
                    link = worktree / relative / name
                    self.assertEqual(Path(os.path.realpath(worktree / "skills" / name)),
                                     Path(os.path.realpath(link)))

    def test_materialization_failure_raises_with_named_reason(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            worktree.mkdir()  # no skills/ tree to project from

            with patch.object(run_tests, "ROOT", root):
                with self.assertRaisesRegex(ValueError, "materialize skill discovery directory"):
                    run_tests.materialize_skill_links(worktree)


class AssuranceCampaignTests(unittest.TestCase):
    def campaign_worktree(self, folder):
        worktree = Path(folder) / "worktree"
        write_file(worktree / "Services/Thing.cs", "class Thing {}\n")
        for relative in UNMEASURED:
            write_file(worktree / relative, "class Unmeasured {}\n")
        for relative in CAMPAIGN_BINARIES:
            write_file(worktree / relative, relative)
        return worktree

    def campaign_reports(self, worktree):
        results = worktree / CAMPAIGN_RESULTS
        write_file(results / "coverage.opencover.xml", "<CoverageSession />")
        write_file(results / "nested/coverage-report.txt", "raw")
        write_file(results / "unrelated.log", "ignored")
        (results / "coverage-spool").mkdir()
        return results

    def test_campaign_identities_take_maintained_sources_binaries_and_reports(self):
        with tempfile.TemporaryDirectory() as folder:
            worktree = self.campaign_worktree(folder)
            results = self.campaign_reports(worktree)

            identities = run_tests._campaign_identities(worktree, results)

            self.assertEqual(sorted(["Services/Thing.cs", *CAMPAIGN_BINARIES,
                                     CAMPAIGN_RESULTS + "/coverage.opencover.xml",
                                     CAMPAIGN_RESULTS + "/nested/coverage-report.txt"]),
                             sorted(Path(item).relative_to(worktree).as_posix()
                                    for item in identities))

    def test_publish_assurance_copies_raw_owner_evidence_and_hashes_each_artifact(self):
        with tempfile.TemporaryDirectory() as folder:
            worktree = self.campaign_worktree(folder)
            results = self.campaign_reports(worktree)
            owner = write_file(Path(folder) / "owner/stdout.log", "campaign output").parent
            published = Path(folder) / "published"

            run_tests._publish_assurance(worktree, results, owner, str(published),
                                         {"complete": True, "subject_status": 0})

            manifest = json.loads((published / "identities.json").read_text(encoding="utf-8"))
            self.assertEqual({"complete": True, "subject_status": 0}, manifest["campaign"])
            self.assertEqual("<CoverageSession />",
                             (published / "raw/coverage.opencover.xml").read_text(encoding="utf-8"))
            self.assertEqual("campaign output",
                             (published / "owner/stdout.log").read_text(encoding="utf-8"))
            source = next(row for row in manifest["artifacts"] if row["path"] == "Services/Thing.cs")
            self.assertEqual(hashlib.sha256((worktree / "Services/Thing.cs").read_bytes()).hexdigest(),
                             source["sha256"])

    def test_publish_assurance_refuses_a_relative_or_already_owned_output(self):
        with tempfile.TemporaryDirectory() as folder:
            worktree = Path(folder) / "worktree"
            existing = Path(folder) / "published"
            existing.mkdir()
            for output in ("published", str(existing)):
                with self.assertRaisesRegex(ValueError, "new absolute owned directory"):
                    run_tests._publish_assurance(worktree, worktree / "results",
                                                 worktree / "owner", output, {})

    def test_assurance_campaign_publishes_evidence_when_the_producer_is_absent(self):
        with tempfile.TemporaryDirectory() as folder:
            worktree = Path(folder) / "worktree"
            worktree.mkdir()
            published = Path(folder) / "published"

            self.assertEqual(2, run_tests._run_assurance_campaign(worktree, [], str(published)))

            manifest = json.loads((published / "identities.json").read_text(encoding="utf-8"))
            self.assertEqual([], manifest["artifacts"])
            self.assertTrue(manifest["campaign"]["cleanup_confirmed"])
            self.assertEqual(2, manifest["campaign"]["subject_status"])

    def test_assurance_campaign_reports_an_incomplete_owner_without_a_subject_status(self):
        with tempfile.TemporaryDirectory() as folder:
            published = Path(folder) / "published"

            self.assertEqual(2, run_tests._run_assurance_campaign(
                Path(folder) / "absent-worktree", [], str(published)))

            manifest = json.loads((published / "identities.json").read_text(encoding="utf-8"))
            self.assertFalse(manifest["campaign"]["complete"])
            self.assertIsNone(manifest["campaign"]["subject_status"])
            self.assertEqual("preflight", manifest["campaign"]["failure_category"])


if __name__ == "__main__":
    unittest.main()
