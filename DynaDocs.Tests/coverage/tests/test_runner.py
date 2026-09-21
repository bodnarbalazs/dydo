"""The isolated runner must not install shell completion in the caller's profile."""
import hashlib
import json
import os
import shutil
import sys
import unittest
import tempfile
import subprocess
from datetime import datetime, timedelta, timezone
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

    def test_rename_out_of_subdirectory_prunes_emptied_source_but_keeps_populated_sibling(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            target = Path(folder) / "worktree"
            write_file(root / "skills/newcat/newname/SKILL.md", "new")
            write_file(target / "skills/oldcat/oldname/SKILL.md", "old")
            write_file(target / "skills/oldcat/sibling/SKILL.md", "sibling")
            status = "R  skills/newcat/newname/SKILL.md\0skills/oldcat/oldname/SKILL.md\0"
            with patch.object(run_tests, "ROOT", root), patch.object(run_tests, "_git", return_value=(status, 0)):
                run_tests.copy_dirty_files(target)
            self.assertFalse((target / "skills/oldcat/oldname").exists(), "emptied rename source directory was not pruned")
            self.assertTrue((target / "skills/oldcat").is_dir(), "a sibling directory that still holds a file must survive")
            self.assertEqual("sibling", (target / "skills/oldcat/sibling/SKILL.md").read_text())
            self.assertEqual("new", (target / "skills/newcat/newname/SKILL.md").read_text())

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
            write_file(worktree / "skills/orchestration/admiral/SKILL.md", "canonical")

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
            write_file(root / "skills/orchestration/admiral/SKILL.md", "canonical")
            write_file(worktree / "skills/orchestration/admiral/SKILL.md", "canonical")
            _make_link(root / ".claude/skills/admiral", root / "skills/orchestration/admiral")

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            link = worktree / ".claude/skills/admiral"
            resolved = Path(os.path.realpath(link))
            self.assertEqual(Path(os.path.realpath(worktree / "skills/orchestration/admiral")), resolved)

    def test_foreign_link_target_outside_skills_is_rebased_onto_a_snapshot_sentinel(self):
        # A link that does not resolve inside skills/ is itself the DR 049 violation; the guard must
        # still fail on it inside the snapshot, so it cannot be dropped or rebased into skills/. But
        # the materialized link must never resolve to the real host path: `remove_worktree` runs
        # `git worktree remove --force` over the snapshot, and git follows a directory junction and
        # deletes what it points at, so a link aimed at real host data would let cleanup destroy it.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            worktree = Path(folder) / "worktree"
            outside = Path(folder) / "outside-target"
            outside.mkdir(parents=True)
            write_file(outside / "canary.txt", "do not delete me")
            write_file(worktree / "skills/orchestration/admiral/SKILL.md", "canonical")
            _make_link(root / ".claude/skills/rogue-link", outside)

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            link = worktree / ".claude/skills/rogue-link"
            resolved = Path(os.path.realpath(link))
            snapshot_skills = Path(os.path.realpath(worktree / "skills"))
            self.assertTrue(resolved.is_relative_to(worktree.resolve()),
                             f"{resolved} must resolve inside the snapshot {worktree}")
            self.assertFalse(resolved.is_relative_to(snapshot_skills),
                              f"{resolved} must resolve outside {snapshot_skills}, or the guard's "
                              "containment check would pass vacuously")
            self.assertNotEqual(Path(os.path.realpath(outside)), resolved)
            self.assertTrue((outside / "canary.txt").exists(), "the real host target must survive untouched")

    def test_absent_source_root_projects_flat_links_not_one_link_per_category(self):
        # skills/ is a category tree (skills/<category>/<skill>/): projecting one link per direct
        # child of skills/ would link the category directories themselves, which the
        # CanonicalSkillTree_HasNoAuthoredHostCopies guard cannot distinguish from a real per-skill
        # projection. This must go red if the function reverts to enumerating direct children.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            layout = {
                "orchestration": ["admiral", "issue-captain", "co-thinker"],
                "engineering": ["code-writer", "prototype"],
                "productivity": ["scout"],
            }
            for category, skill_names in layout.items():
                for name in skill_names:
                    write_file(worktree / "skills" / category / name / "SKILL.md", "canonical")

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            all_skills = sorted(name for names in layout.values() for name in names)
            for relative in (".claude/skills", ".agents/skills"):
                projected = sorted(p.name for p in (worktree / relative).iterdir())
                self.assertEqual(all_skills, projected,
                                  "must project one flat link per skill, not one per category")
                for category in layout:
                    self.assertNotIn(category, projected,
                                      "the category directory itself must never be projected")
                for category, skill_names in layout.items():
                    for name in skill_names:
                        link = worktree / relative / name
                        self.assertEqual(
                            Path(os.path.realpath(worktree / "skills" / category / name)),
                            Path(os.path.realpath(link)))

    def test_absent_source_root_projects_all_29_real_repo_skills_flat(self):
        # The real repo's category tree (orchestration 8, engineering 10, productivity 11) must
        # project 29 flat links, proving the small synthetic fixture above generalizes.
        real_root = run_tests.ROOT
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            shutil.copytree(real_root / "skills", worktree / "skills")

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            for relative in (".claude/skills", ".agents/skills"):
                projected = list((worktree / relative).iterdir())
                self.assertEqual(29, len(projected))
                for category in ("orchestration", "engineering", "productivity"):
                    self.assertFalse((worktree / relative / category).exists())

    def test_materialization_failure_raises_with_named_reason(self):
        # The snapshot *has* a skills/ tree, but it is empty: there is nothing to project, and this
        # must stay loud rather than widen into the no-op that a wholly absent skills/ tree gets
        # below, or the guard's blind spot would come back.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            (worktree / "skills").mkdir(parents=True)  # skills/ exists but has no canonical skills

            with patch.object(run_tests, "ROOT", root):
                with self.assertRaisesRegex(ValueError, "materialize skill discovery directory"):
                    run_tests.materialize_skill_links(worktree)

    def test_category_child_without_skill_md_raises_the_way_setup_skills_mjs_does(self):
        # setup-skills.mjs:56 throws "Canonical skill is missing SKILL.md: <category>/<name>" and
        # projects nothing at all. The python walk must fail closed the same way instead of
        # silently dropping the entry, or a category directory with no SKILL.md would still yield a
        # green, plausible-looking per-skill projection here while node refuses to run at all.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            write_file(worktree / "skills/engineering/code-writer/SKILL.md", "canonical")
            (worktree / "skills/engineering/no-skill-md").mkdir(parents=True)

            with patch.object(run_tests, "ROOT", root):
                with self.assertRaisesRegex(ValueError, r"engineering/no-skill-md"):
                    run_tests.materialize_skill_links(worktree)

    def test_category_with_no_skills_raises_naming_the_category_root(self):
        # A category directory that holds no skills must stay loud: no test previously reached
        # _canonical_skill_names's own "no canonical skills found in <category_root>" raise (the
        # existing coverage only reached the outer, whole-tree-empty case).
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            write_file(worktree / "skills/engineering/code-writer/SKILL.md", "canonical")
            (worktree / "skills/orchestration").mkdir(parents=True)

            with patch.object(run_tests, "ROOT", root):
                with self.assertRaisesRegex(ValueError, r"no canonical skills found in.*orchestration"):
                    run_tests.materialize_skill_links(worktree)

    def test_duplicate_skill_name_across_categories_raises_naming_both_categories(self):
        # The same skill name claimed by two categories must stay loud: no test previously reached
        # _canonical_skill_names's own "duplicate skill name across categories" raise.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            write_file(worktree / "skills/engineering/scout/SKILL.md", "canonical")
            write_file(worktree / "skills/orchestration/scout/SKILL.md", "canonical")

            with patch.object(run_tests, "ROOT", root):
                with self.assertRaisesRegex(ValueError, r"duplicate skill name across categories: scout \(engineering and orchestration\)"):
                    run_tests.materialize_skill_links(worktree)

    def test_absent_snapshot_skills_tree_and_absent_source_root_is_a_quiet_noop(self):
        # No canonical skills to project and no host discovery directory to mirror: there is
        # nothing the CanonicalSkillTree_HasNoAuthoredHostCopies guard could catch either way, so
        # this must complete without raising and without creating anything.
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder) / "source"
            root.mkdir()
            worktree = Path(folder) / "worktree"
            worktree.mkdir()  # no skills/ tree at all

            with patch.object(run_tests, "ROOT", root):
                run_tests.materialize_skill_links(worktree)

            for relative in (".claude/skills", ".agents/skills"):
                self.assertFalse((worktree / relative).exists())


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


class WorktreeCleanupTests(unittest.TestCase):
    """The runner registers a marker before running anything, and cleans up on every exit path."""

    def _isolate_run_tests(self, folder):
        """Patch run_tests() down to: real candidate allocation under `folder`, a fake git worktree
        add/remove, and no real dotnet/skill/dirty-copy work -- so only cleanup ordering is tested.
        """
        return (
            patch.object(run_tests, "prune_stale_test_worktrees"),
            patch.object(run_tests.tempfile, "gettempdir", return_value=folder),
            patch.object(run_tests.uuid, "uuid4", return_value=type("U", (), {"hex": "cafefeed"})()),
            patch.object(run_tests, "is_registered_worktree", return_value=False),
            patch.object(run_tests, "create_worktree", return_value=True),
            patch.object(run_tests, "copy_dirty_files"),
            patch.object(run_tests, "materialize_skill_links"),
        )

    def test_cleanup_runs_when_the_test_subprocess_exits_nonzero(self):
        with tempfile.TemporaryDirectory() as folder:
            patches = self._isolate_run_tests(folder)
            with patches[0], patches[1], patches[2], patches[3], patches[4], patches[5], patches[6], \
                 patch.object(run_tests, "remove_worktree") as remove, \
                 patch.object(run_tests.subprocess, "run") as run:
                run.return_value.returncode = 1

                rc = run_tests.run_tests()

            self.assertEqual(1, rc)
            remove.assert_called_once()
            self.assertEqual(Path(folder) / "dydo-test-cafefeed", remove.call_args.args[0])

    def test_cleanup_runs_on_a_simulated_interrupt_from_the_test_subprocess(self):
        with tempfile.TemporaryDirectory() as folder:
            patches = self._isolate_run_tests(folder)
            with patches[0], patches[1], patches[2], patches[3], patches[4], patches[5], patches[6], \
                 patch.object(run_tests, "remove_worktree") as remove, \
                 patch.object(run_tests.subprocess, "run", side_effect=KeyboardInterrupt):
                with self.assertRaises(KeyboardInterrupt):
                    run_tests.run_tests()

            remove.assert_called_once()

    def test_marker_exists_before_the_test_command_is_invoked(self):
        observed = {}

        def observing_copy_dirty_files(worktree):
            observed["marker_present_at_copy"] = (worktree / run_tests.MARKER_NAME).exists()
            observed["worktree"] = worktree

        with tempfile.TemporaryDirectory() as folder:
            with patch.object(run_tests, "prune_stale_test_worktrees"), \
                 patch.object(run_tests.tempfile, "gettempdir", return_value=folder), \
                 patch.object(run_tests.uuid, "uuid4", return_value=type("U", (), {"hex": "cafefeed"})()), \
                 patch.object(run_tests, "is_registered_worktree", return_value=False), \
                 patch.object(run_tests, "create_worktree", return_value=True), \
                 patch.object(run_tests, "copy_dirty_files", side_effect=observing_copy_dirty_files), \
                 patch.object(run_tests, "materialize_skill_links"), \
                 patch.object(run_tests, "remove_worktree"), \
                 patch.object(run_tests.subprocess, "run") as run:
                run.return_value.returncode = 0

                run_tests.run_tests()

            self.assertTrue(observed.get("marker_present_at_copy"))
            marker = json.loads((observed["worktree"] / run_tests.MARKER_NAME).read_text(encoding="utf-8"))
            self.assertEqual(1, marker["schema"])
            self.assertEqual(os.getpid(), marker["pid"])
            datetime.strptime(marker["createdAtUtc"], "%Y-%m-%dT%H:%M:%SZ")

    def test_remove_worktree_unlocks_then_forces_removal_then_prunes_in_order(self):
        calls = []

        def fake_git(*args, capture=False):
            calls.append(args)
            return ("", 0) if capture else None

        with tempfile.TemporaryDirectory() as folder:
            worktree = Path(folder) / "wt"
            worktree.mkdir()
            with patch.object(run_tests, "_git", side_effect=fake_git):
                run_tests.remove_worktree(worktree)

        self.assertEqual(("worktree", "unlock", str(worktree)), calls[0])
        self.assertEqual(("worktree", "remove", "--force", str(worktree)), calls[1])
        self.assertIn(("worktree", "prune"), calls)
        unlock_index = calls.index(("worktree", "unlock", str(worktree)))
        force_index = calls.index(("worktree", "remove", "--force", str(worktree)))
        prune_index = calls.index(("worktree", "prune"))
        self.assertLess(unlock_index, force_index)
        self.assertLess(force_index, prune_index)

    def test_remove_worktree_captures_the_unlock_call_so_a_clean_run_prints_no_fatal_line(self):
        # An unlock on an already-unlocked worktree is expected to fail with "fatal: ... is not
        # locked" on every clean run. Uncaptured, that prints straight to the console -- a false
        # "fatal" in every gate record. Capturing it keeps the call's outcome silent and ignored.
        calls = []

        def fake_git(*args, capture=False):
            calls.append((args, capture))
            return ("", 0) if capture else None

        with tempfile.TemporaryDirectory() as folder:
            worktree = Path(folder) / "wt"
            worktree.mkdir()
            with patch.object(run_tests, "_git", side_effect=fake_git):
                run_tests.remove_worktree(worktree)

        unlock_call = next(call for call in calls if call[0] == ("worktree", "unlock", str(worktree)))
        self.assertTrue(unlock_call[1], "the unlock call must pass capture=True")


class StaleWorktreeAgeOverrideTests(unittest.TestCase):
    """An override the caller set but got wrong is a boundary error, never a silent fallback."""

    def test_unparseable_override_raises_naming_the_variable_and_value(self):
        with patch.dict(os.environ, {run_tests.STALE_WORKTREE_AGE_ENV: "soon"}):
            with self.assertRaisesRegex(ValueError, f"{run_tests.STALE_WORKTREE_AGE_ENV}.*'soon'"):
                run_tests._stale_worktree_max_age_seconds()

    def test_non_positive_override_raises_naming_the_variable_and_value(self):
        for value in ("0", "-5"):
            with patch.dict(os.environ, {run_tests.STALE_WORKTREE_AGE_ENV: value}):
                with self.assertRaisesRegex(ValueError, run_tests.STALE_WORKTREE_AGE_ENV):
                    run_tests._stale_worktree_max_age_seconds()

    def test_non_finite_override_raises_naming_the_variable_and_value(self):
        for value in ("nan", "inf"):
            with patch.dict(os.environ, {run_tests.STALE_WORKTREE_AGE_ENV: value}):
                with self.assertRaisesRegex(ValueError, run_tests.STALE_WORKTREE_AGE_ENV):
                    run_tests._stale_worktree_max_age_seconds()

    def test_valid_positive_override_is_used(self):
        with patch.dict(os.environ, {run_tests.STALE_WORKTREE_AGE_ENV: "120"}):
            self.assertEqual(120.0, run_tests._stale_worktree_max_age_seconds())

    def test_absent_override_uses_the_default(self):
        environ_without_override = {k: v for k, v in os.environ.items()
                                     if k != run_tests.STALE_WORKTREE_AGE_ENV}
        with patch.dict(os.environ, environ_without_override, clear=True):
            self.assertEqual(run_tests.STALE_TEST_WORKTREE_SECONDS,
                              run_tests._stale_worktree_max_age_seconds())


class StaleWorktreePruningTests(unittest.TestCase):
    """Startup pruning removes only a marked, stale, dydo-test-* worktree under temp."""

    def _mark(self, path, age_seconds):
        path.mkdir(parents=True, exist_ok=True)
        stamp = datetime.now(timezone.utc) - timedelta(seconds=age_seconds)
        run_tests.write_worktree_marker(path, created_at=stamp)

    def test_prune_removes_only_the_stale_marked_registered_temp_worktree(self):
        with tempfile.TemporaryDirectory() as temp_root_str, \
             tempfile.TemporaryDirectory() as outside_root_str:
            temp_root = Path(temp_root_str)
            outside_root = Path(outside_root_str)

            stale = temp_root / "dydo-test-stale0001"
            young = temp_root / "dydo-test-young0001"
            unmarked = temp_root / "dydo-test-unmarked01"
            outside = outside_root / "dydo-test-outside001"

            self._mark(stale, age_seconds=99999)
            self._mark(young, age_seconds=10)
            unmarked.mkdir()
            self._mark(outside, age_seconds=99999)

            porcelain = "".join(f"worktree {p}\n\n" for p in (stale, young, unmarked, outside))

            def fake_git(*args, capture=False):
                if args[:2] == ("worktree", "list"):
                    return porcelain, 0
                return ("", 0) if capture else None

            with patch.object(run_tests, "tempfile") as fake_tempfile, \
                 patch.object(run_tests, "_git", side_effect=fake_git):
                fake_tempfile.gettempdir.return_value = str(temp_root)

                pruned = run_tests.prune_stale_test_worktrees(max_age_seconds=3600)

            pruned_names = {p.name for p in pruned}
            self.assertIn("dydo-test-stale0001", pruned_names)
            self.assertNotIn("dydo-test-young0001", pruned_names)
            self.assertNotIn("dydo-test-unmarked01", pruned_names)
            self.assertNotIn("dydo-test-outside001", pruned_names)
            self.assertFalse(stale.exists())
            self.assertTrue(young.exists())
            self.assertTrue(unmarked.exists())
            self.assertTrue(outside.exists())

    def test_prune_removes_a_stale_marked_leftover_directory_git_no_longer_registers(self):
        with tempfile.TemporaryDirectory() as temp_root_str:
            temp_root = Path(temp_root_str)
            leftover = temp_root / "dydo-test-leftover01"
            self._mark(leftover, age_seconds=99999)

            def fake_git(*args, capture=False):
                if args[:2] == ("worktree", "list"):
                    return "", 0
                return ("", 0) if capture else None

            with patch.object(run_tests, "tempfile") as fake_tempfile, \
                 patch.object(run_tests, "_git", side_effect=fake_git):
                fake_tempfile.gettempdir.return_value = str(temp_root)

                pruned = run_tests.prune_stale_test_worktrees(max_age_seconds=3600)

            self.assertEqual([leftover.resolve()], [p.resolve() for p in pruned])
            self.assertFalse(leftover.exists())


if __name__ == "__main__":
    unittest.main()
