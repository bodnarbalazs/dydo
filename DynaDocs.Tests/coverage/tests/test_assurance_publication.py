"""Assurance publication writes only into the directory it created and still holds.

Every replacement here is a real rename, link or hard link, made by a trace at the first
publication line after its trigger appears on disk: the earliest point another process could act.
"""
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import run_tests

TOOLS = os.path.normcase(os.path.dirname(os.path.abspath(run_tests.__file__)))
RESULTS = "dydo/_system/.local/csharp-campaign-results"
BINARIES = ["bin/Debug/net10.0/dydo.dll", "bin/Debug/net10.0/dydo.pdb",
            "DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.dll",
            "DynaDocs.Tests/bin/Debug/net10.0/DynaDocs.Tests.pdb",
            "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.dll",
            "DynaDocs.Tests/coverage/metrics/bin/Debug/net10.0/GateMetrics.pdb"]
CAMPAIGN = {"complete": True, "cleanup_confirmed": True, "subject_status": 0}
FOREIGN = b'{"foreign": true}\n'
FIRST_RAW = "raw/coverage.opencover.xml"


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)
    return path


def tree(folder):
    """Every entry under `folder` by relative path: a file's bytes, or None for a directory."""
    return {path.relative_to(folder).as_posix(): None if path.is_dir() else path.read_bytes()
            for path in Path(folder).rglob("*")}


def link(path, target):
    """Make `path` a directory link to `target`: a junction on Windows, where a symlink needs a
    privilege."""
    if os.name == "nt":
        import _winapi
        _winapi.CreateJunction(str(target), str(path))
    else:
        path.symlink_to(target, target_is_directory=True)


def unlink(path):
    """Remove the directory link `path`, never its target."""
    if os.name == "nt":
        os.rmdir(path)
    else:
        path.unlink()


def campaign(root):
    """A worktree whose campaign reports, nested ones included, and owner evidence publish."""
    worktree = root / "worktree"
    for relative in ("Services/Thing.cs", *BINARIES):
        write(worktree / relative, relative.encode())
    write(worktree / RESULTS / "coverage.opencover.xml", b"<CoverageSession />")
    write(worktree / RESULTS / "nested/coverage-report.txt", b"raw")
    write(worktree / "owner/stdout.log", b"campaign output")
    return worktree


def manifest(path):
    return json.loads(path.read_bytes())


class AssurancePublicationTests(unittest.TestCase):
    def publish(self, worktree, output, ready, attack):
        """Real publication of `worktree`'s campaign to `output`, running `attack` once at the first
        publication line after `ready()` holds. Returns the attack's result and the OSError
        publication raised, or None."""
        fired = []

        def trace(frame, event, _arg):
            if os.path.normcase(os.path.dirname(frame.f_code.co_filename)) != TOOLS:
                return None
            if event == "line" and not fired and ready():
                fired.append(attack())
            return trace

        error = None
        previous = sys.gettrace()
        sys.settrace(trace)
        try:
            run_tests._publish_assurance(worktree, worktree / RESULTS, worktree / "owner",
                                         str(output), dict(CAMPAIGN))
        except OSError as raised:
            error = raised
        finally:
            sys.settrace(previous)
        self.assertEqual(1, len(fired), "the attack never ran")
        return fired[0], error

    def test_publication_copies_every_tree_and_writes_the_manifest_as_text(self):
        with tempfile.TemporaryDirectory() as folder:
            worktree, output = campaign(Path(folder)), Path(folder) / "published"

            run_tests._publish_assurance(worktree, worktree / RESULTS, worktree / "owner",
                                         str(output), dict(CAMPAIGN))

            self.assertEqual(tree(worktree / RESULTS), tree(output / "raw"))
            self.assertEqual(tree(worktree / "owner"), tree(output / "owner"))
            self.assertEqual({"raw", "owner", "identities.json"}, {path.name for path in output.iterdir()})
            text = json.dumps(manifest(output / "identities.json"), indent=2, sort_keys=True) + "\n"
            self.assertEqual(text.replace("\n", os.linesep).encode("utf-8"),
                             (output / "identities.json").read_bytes())

    def test_an_output_replaced_after_creation_or_mid_write_is_never_written_through(self):
        windows = {"after creation": lambda output: output,
                   "while raw/ is copied": lambda output: output / FIRST_RAW}
        for replacement in ("directory", "directory link"):
            for window, trigger in windows.items():
                with self.subTest(replacement=replacement, window=window), \
                        tempfile.TemporaryDirectory() as folder:
                    root = Path(folder)
                    worktree, output = campaign(root), root / "published"
                    foreign = write(root / "foreign/identities.json", FOREIGN).parent
                    before = tree(foreign)

                    def replace():
                        try:
                            output.rename(root / "ours")
                        except PermissionError:
                            return "refused"
                        if replacement == "directory":
                            foreign.rename(output)
                        else:
                            link(output, foreign)
                        return "replaced"

                    outcome, error = self.publish(
                        worktree, output, lambda: os.path.lexists(trigger(output)), replace)

                    moved = outcome == "replaced" and replacement == "directory"
                    self.assertEqual(before, tree(output if moved else foreign),
                                     "publication wrote into the foreign directory")
                    if os.name == "nt":
                        # The held output shares no delete access, so nothing can rename it away.
                        self.assertEqual(("refused", None), (outcome, error))
                        self.assertEqual(CAMPAIGN, manifest(output / "identities.json")["campaign"])
                    else:
                        self.assertEqual("replaced", outcome)
                        self.assertIn(str(output), str(error))

    def test_publication_fails_closed_when_a_parent_link_is_redirected_or_removed_mid_write(self):
        for change in ("redirected", "removed"):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                worktree, held, parent = campaign(root), root / "held", root / "parent"
                foreign = write(root / "elsewhere/published/identities.json", FOREIGN).parent
                held.mkdir()
                link(parent, held)
                output = parent / "published"
                before = tree(foreign)

                def change_link():
                    unlink(parent)
                    if change == "redirected":
                        link(parent, foreign.parent)
                    return change

                _, error = self.publish(worktree, output, lambda: (output / FIRST_RAW).exists(),
                                        change_link)

                self.assertEqual(before, tree(foreign), "publication wrote into the foreign directory")
                self.assertIn(str(output), str(error))
                # Every write after the change still reached the directory publication created.
                self.assertEqual(CAMPAIGN, manifest(held / "published/identities.json")["campaign"])

    def test_a_manifest_name_planted_in_the_output_is_never_written_through(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            worktree, output = campaign(root), root / "published"
            foreign = write(root / "foreign.json", FOREIGN)

            def plant():
                os.link(foreign, output / "identities.json")  # a second name for the foreign file
                return "planted"

            _, error = self.publish(worktree, output, lambda: (output / "owner/stdout.log").exists(),
                                    plant)

            self.assertEqual(FOREIGN, foreign.read_bytes(), "publication wrote through the planted name")
            self.assertIsInstance(error, FileExistsError)


if __name__ == "__main__":
    unittest.main()
