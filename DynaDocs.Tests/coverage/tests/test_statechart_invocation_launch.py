"""Exercise the actual hidden wrapper with a harmless owned file driver."""
import contextlib
import io
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('_dyd415_invocation_launch', Path(__file__).resolve().parents[3] /
    'dydo/project/pitfalls/dyd-415-statechart-evidence/native-invocation/implementation2/launch.py')
launch = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(launch)


class LaunchTests(unittest.TestCase):
    def test_real_driver_copy_preserves_argv_streams_and_exit_without_shell(self):
        with tempfile.TemporaryDirectory(prefix='dyd415-launch-test-') as folder:
            root = Path(folder)
            source = root / 'driver.py'
            source.write_text('import sys\nprint(repr(sys.argv[1:]))\nprint("launch error", file=sys.stderr)\nsys.exit(9)\n')
            stdout, stderr = io.StringIO(), io.StringIO()
            real_run = subprocess.run
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                with patch.object(launch.subprocess, 'run', wraps=real_run) as run:
                    result = launch.main(['request', 'spaced-attempt'], here=root, temp=root)
            self.assertEqual(9, result)
            self.assertEqual(subprocess.CREATE_NO_WINDOW, run.call_args.kwargs['creationflags'])
            self.assertEqual(['request', 'spaced-attempt'], run.call_args.args[0][-2:])
            self.assertIn("['request', 'spaced-attempt']", stdout.getvalue())
            self.assertIn('launch error', stderr.getvalue())
            copied = list(root.glob('dyd415-implementation2-driver-*'))
            self.assertEqual(1, len(copied))
            self.assertEqual(source.read_bytes(), (copied[0] / 'driver.py').read_bytes())

    def test_invalid_mode_or_arity_creates_no_output_and_starts_no_child(self):
        with patch.object(launch.tempfile, 'mkdtemp') as fresh:
            for arguments in [[], ['request'], ['unknown', 'attempt'], ['request', 'a', 'extra']]:
                with self.subTest(arguments=arguments), self.assertRaises(AssertionError):
                    launch.main(arguments)
            fresh.assert_not_called()


if __name__ == '__main__':
    unittest.main()
