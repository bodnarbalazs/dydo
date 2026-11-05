"""Coverage unions consume each platform leg's latest-attempt artifact and fail closed on anything else."""
import contextlib
from fnmatch import fnmatchcase
import io
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import coverage_artifacts
from coverage_artifacts import SelectionError, artifact_name, select, stamp

RUN = 42038108870
COMMIT = '4620994'
WORKFLOW = Path(__file__).resolve().parents[3] / '.github' / 'workflows' / 'ci.yml'


class SelectionTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)
        self.staging = self.root / 'coverage-legs'
        self.destinations = {platform: self.root / 'coverage-platforms' / platform
                             for platform in coverage_artifacts.PLATFORMS}

    def leg(self, platform, attempt, *, stack='python', commit=COMMIT, name=None, adapter=True):
        folder = self.staging / (name or artifact_name(stack, platform, attempt))
        stamp(folder, stack, platform, RUN, attempt, commit)
        if adapter:
            (folder / 'adapters').mkdir()
            (folder / 'adapters' / 'python-coverage.json').write_text(f'{platform}-{attempt}', encoding='utf-8')
        return folder

    def select(self, stack='python', attempt=2):
        return select(self.staging, stack, RUN, attempt, self.destinations)

    def assertRefused(self, message, stack='python', attempt=2):
        with self.assertRaisesRegex(SelectionError, message):
            self.select(stack, attempt)
        self.assertFalse(any(path.exists() for path in self.destinations.values()))

    def test_rerun_of_failed_leg_takes_each_legs_latest_attempt(self):
        # The incident: attempt 1 Windows failed without an adapter report; rerun --failed reran only Windows.
        self.leg('linux', 1)
        self.leg('windows', 1, adapter=False)
        self.leg('windows', 2)
        receipt = self.select()
        self.assertEqual({'schema': 1, 'stack': 'python', 'run_id': RUN, 'run_attempt': 2, 'commit': COMMIT,
                          'legs': {'linux': {'artifact': 'python-coverage-linux-attempt-1', 'attempt': 1,
                                             'superseded': []},
                                   'windows': {'artifact': 'python-coverage-windows-attempt-2', 'attempt': 2,
                                               'superseded': ['python-coverage-windows-attempt-1']}}}, receipt)
        for platform, attempt in (('linux', 1), ('windows', 2)):
            self.assertEqual(f'{platform}-{attempt}', (self.destinations[platform] / 'adapters'
                                                       / 'python-coverage.json').read_text(encoding='utf-8'))
        self.assertEqual(['python-coverage-windows-attempt-1'], [p.name for p in self.staging.iterdir()])

    def test_latest_attempt_wins_whatever_the_staging_order(self):
        for attempt in (3, 1, 10, 2):
            self.leg('windows', attempt)
        self.leg('linux', 1)
        receipt = self.select(attempt=10)
        self.assertEqual(10, receipt['legs']['windows']['attempt'])
        self.assertEqual([artifact_name('python', 'windows', n) for n in (1, 2, 3)],
                         receipt['legs']['windows']['superseded'])

    def test_dotnet_union_selects_its_own_stack(self):
        self.leg('linux', 1, stack='dotnet')
        self.leg('windows', 2, stack='dotnet')
        receipt = self.select('dotnet')
        self.assertEqual('dotnet-coverage-windows-attempt-2', receipt['legs']['windows']['artifact'])

    def test_missing_leg_fails_closed(self):
        self.assertRefused('Missing python linux leg artifact python-coverage-linux-attempt-<attempt>')
        self.leg('windows', 1)
        self.assertRefused('Missing python linux leg artifact')

    def test_union_other_stack_or_bare_artifact_fails_closed(self):
        self.leg('linux', 1)
        self.leg('windows', 1)
        for name in ('python-coverage-union', 'python-coverage-windows', 'dotnet-coverage-linux-attempt-1',
                     'python-coverage-macos-attempt-1', 'python-coverage-linux-attempt-x'):
            with self.subTest(name=name):
                (self.staging / name).mkdir()
                self.assertRefused(f'Unexpected artifact {name!r}; a python union consumes only '
                                   'python-coverage-<platform>-attempt-<attempt> leg artifacts')
                (self.staging / name).rmdir()
        (self.staging / 'python-coverage-linux-attempt-2').write_text('', encoding='utf-8')
        self.assertRefused('Unexpected artifact')

    def test_duplicate_leg_attempt_fails_closed(self):
        self.leg('linux', 1)
        self.leg('windows', 2)
        self.leg('windows', 2, name='python-coverage-windows-attempt-02')
        self.assertRefused('Duplicate windows artifacts for attempt 2: '
                           'python-coverage-windows-attempt-02 and python-coverage-windows-attempt-2')

    def test_attempt_newer_than_the_run_fails_closed(self):
        self.leg('linux', 1)
        self.leg('windows', 3)
        self.assertRefused('python-coverage-windows-attempt-3 is newer than run attempt 2')

    def test_identity_that_does_not_match_its_leg_fails_closed(self):
        self.leg('linux', 1)
        windows = self.leg('windows', 2)
        recorded = json.loads((windows / coverage_artifacts.IDENTITY).read_text(encoding='utf-8'))
        for field, value in (('run_attempt', 1), ('platform', 'linux'), ('stack', 'dotnet'), ('run_id', RUN + 1),
                             ('artifact', 'python-coverage-windows-attempt-1'), ('schema', 2), ('commit', ''),
                             ('commit', None), ('extra', True)):
            with self.subTest(field=field, value=value):
                (windows / coverage_artifacts.IDENTITY).write_text(json.dumps({**recorded, field: value}),
                                                                   encoding='utf-8')
                self.assertRefused('python-coverage-windows-attempt-2 identity .* does not match its leg, '
                                   f'run {RUN} and attempt 2')
        for content in ('[]', '"commit"'):
            (windows / coverage_artifacts.IDENTITY).write_text(content, encoding='utf-8')
            self.assertRefused('does not match its leg')

    def test_unreadable_identity_fails_closed(self):
        self.leg('linux', 1)
        windows = self.leg('windows', 2)
        (windows / coverage_artifacts.IDENTITY).write_text('{', encoding='utf-8')
        self.assertRefused(f'python-coverage-windows-attempt-2 has no readable {coverage_artifacts.IDENTITY}')
        (windows / coverage_artifacts.IDENTITY).unlink()
        self.assertRefused(f'python-coverage-windows-attempt-2 has no readable {coverage_artifacts.IDENTITY}')

    def test_legs_from_different_candidates_fail_closed(self):
        self.leg('linux', 1)
        self.leg('windows', 2, commit='1a351bb1')
        self.assertRefused("Leg artifacts come from different candidates: "
                           "{'linux': '4620994', 'windows': '1a351bb1'}")

    def test_occupied_destination_fails_closed_before_moving_anything(self):
        self.leg('linux', 1)
        self.leg('windows', 2)
        self.destinations['windows'].mkdir(parents=True)
        with self.assertRaisesRegex(SelectionError, 'Selected artifact destinations already exist'):
            self.select()
        self.assertFalse(self.destinations['linux'].exists())
        self.assertEqual(2, len(list(self.staging.iterdir())))


class CommandTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)

    def run_main(self, *argv):
        stdout, stderr = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            code = coverage_artifacts.main([str(value) for value in argv])
        return code, stdout.getvalue(), stderr.getvalue()

    def select_argv(self, run_attempt):
        return ('select', '--stack', 'dotnet', '--run-id', RUN, '--run-attempt', run_attempt,
                '--staging', self.root / 'legs', '--linux', self.root / 'union' / 'linux',
                '--windows', self.root / 'union' / 'windows', '--receipt', self.root / 'union' / 'selection.json')

    def test_stamped_legs_select_with_a_receipt_and_refusals_exit_two(self):
        head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        for platform, attempt in (('linux', 1), ('windows', 1), ('windows', 2)):
            code, _, _ = self.run_main('stamp', '--stack', 'dotnet', '--platform', platform, '--run-id', RUN,
                                       '--run-attempt', attempt, '--results',
                                       self.root / 'legs' / artifact_name('dotnet', platform, attempt))
            self.assertEqual(0, code)
        stamped = json.loads((self.root / 'legs' / 'dotnet-coverage-windows-attempt-2'
                              / coverage_artifacts.IDENTITY).read_text(encoding='utf-8'))
        self.assertEqual(coverage_artifacts.identity('dotnet', 'windows', RUN, 2, head), stamped)

        code, _, stderr = self.run_main(*self.select_argv(1))
        self.assertEqual(2, code)
        self.assertIn('dotnet-coverage-windows-attempt-2 is newer than run attempt 1', stderr)
        self.assertFalse((self.root / 'union').exists())

        code, stdout, _ = self.run_main(*self.select_argv(2))
        self.assertEqual(0, code)
        receipt = json.loads((self.root / 'union' / 'selection.json').read_text(encoding='utf-8'))
        self.assertEqual(receipt, json.loads(stdout))
        self.assertEqual({'linux': 1, 'windows': 2}, {p: leg['attempt'] for p, leg in receipt['legs'].items()})
        self.assertEqual(stamped, json.loads((self.root / 'union' / 'windows'
                                              / coverage_artifacts.IDENTITY).read_text(encoding='utf-8')))


def _job(workflow, name):
    return re.search(rf'^  {re.escape(name)}:\n(.*?)(?=^  \S|\Z)', workflow, re.M | re.S)[1]


def _mapping(lines, depth):
    """Keys at this indentation; with: and env: nest, any other value's lines join with spaces."""
    fields = {}
    for line in lines:
        field = re.match(rf' {{{depth}}}([\w-]+):(?: (.*))?$', line)
        if field:
            key = field[1]
            fields[key] = [field[2] or '']
        else:
            fields[key].append(line)
    return {key: _mapping(value[1:], depth + 2) if key in ('with', 'env')
            else ' '.join(part.strip() for part in value if part.strip() not in ('', '>-', '|'))
            for key, value in fields.items()}


def _steps(job):
    blocks = re.split(r'^      - ', job.split('\n    steps:\n', 1)[1], flags=re.M)[1:]
    return [_mapping(('        ' + block).splitlines(), 8) for block in blocks]


def _uses(steps, action):
    return [index for index, step in enumerate(steps) if step.get('uses', '').startswith(action)]


def _named(steps, name):
    return [step.get('name') for step in steps].index(name)


def _arguments(run):
    return dict(re.findall(r'--([\w-]+) (\S+)', run))


LEGS = {'python': ('python-coverage', 'python-coverage-union', 'Upload platform coverage evidence',
                   'Stamp platform coverage artifact identity', 'Verify complete same-candidate platform union',
                   'python-coverage-{platform}'),
        'dotnet': ('dotnet-coverage', 'dotnet-coverage-union', 'Upload platform .NET coverage evidence',
                   'Stamp platform .NET coverage artifact identity',
                   'Verify the complete same-candidate platform partition', '{platform}')}
RESULTS = 'DynaDocs.Tests/coverage/results'


def assert_rerun_safe_coverage_artifacts(test, workflow, stack):
    leg_job, union_job, upload, stamp_step, verify, folder = LEGS[stack]
    leg = _steps(_job(workflow, leg_job))
    uploads = _uses(leg, 'actions/upload-artifact@')
    test.assertEqual([_named(leg, upload)], uploads)
    test.assertEqual(f'{stack}-coverage-${{{{ matrix.platform }}}}-attempt-${{{{ github.run_attempt }}}}',
                     leg[uploads[0]]['with']['name'])
    test.assertEqual(RESULTS, leg[uploads[0]]['with']['path'])
    test.assertEqual(_named(leg, stamp_step) + 1, uploads[0])
    for step in (leg[uploads[0]], leg[uploads[0] - 1]):
        test.assertEqual('always()', step.get('if'))
    test.assertTrue(leg[uploads[0] - 1]['run'].startswith('python DynaDocs.Tests/coverage/coverage_artifacts.py stamp '))
    test.assertEqual({'stack': stack, 'platform': '${{', 'run-id': '${{', 'run-attempt': '${{', 'results': RESULTS},
                     _arguments(leg[uploads[0] - 1]['run']))

    union = _steps(_job(workflow, union_job))
    downloads = _uses(union, 'actions/download-artifact@')
    test.assertEqual(1, len(downloads))
    download = union[downloads[0]]['with']
    test.assertEqual(f'{stack}-coverage-*-attempt-*', download.get('pattern'))
    test.assertEqual({'name', 'artifact-ids'} & set(download), set())
    test.assertEqual('false', download.get('merge-multiple'))
    test.assertEqual(f'{RESULTS}/coverage-legs', download['path'])
    for name in (f'{stack}-coverage-linux-attempt-1', f'{stack}-coverage-windows-attempt-12'):
        test.assertTrue(fnmatchcase(name, download['pattern']), name)
    for name in (f'{stack}-coverage-union', f'{stack}-coverage-windows', 'python-coverage-union'):
        test.assertFalse(fnmatchcase(name, download['pattern']), name)

    selection = _named(union, "Select each leg's latest-attempt artifact")
    test.assertTrue(downloads[0] < _named(union, 'Require both native jobs to pass') < selection
                    < _named(union, verify))
    test.assertNotIn('if', union[selection])
    run = union[selection]['run']
    test.assertTrue(run.startswith('python DynaDocs.Tests/coverage/coverage_artifacts.py select '))
    test.assertIn('--run-id ${{ github.run_id }} --run-attempt ${{ github.run_attempt }}', run)
    arguments = _arguments(run)
    test.assertEqual((stack, download['path'], f'{RESULTS}/coverage-platforms/artifact-selection.json'),
                     (arguments['stack'], arguments['staging'], arguments['receipt']))
    verified = _arguments(union[_named(union, verify)]['run'])
    for platform in coverage_artifacts.PLATFORMS:
        destination = f'{RESULTS}/coverage-platforms/{folder.format(platform=platform)}'
        test.assertEqual(destination, arguments[platform])
        test.assertEqual(f'{destination}/adapters/{stack}-coverage.json', verified[platform])


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.workflow = WORKFLOW.read_text(encoding='utf-8').replace('\r\n', '\n')

    def test_both_unions_download_attempt_qualified_legs_and_select_the_latest(self):
        for stack in coverage_artifacts.STACKS:
            with self.subTest(stack=stack):
                assert_rerun_safe_coverage_artifacts(self, self.workflow, stack)

    def test_stale_prone_downloads_and_unqualified_uploads_are_rejected(self):
        for stack, original, replacement in (
                ('python', 'pattern: python-coverage-*-attempt-*', 'pattern: python-coverage-*'),
                ('python', '-attempt-${{ github.run_attempt }}\n', '\n'),
                ('python', '          merge-multiple: false\n', '          merge-multiple: true\n'),
                ('dotnet', 'pattern: dotnet-coverage-*-attempt-*', 'name: dotnet-coverage-windows'),
                ('dotnet', 'pattern: dotnet-coverage-*-attempt-*\n',
                 'pattern: dotnet-coverage-*-attempt-*\n          artifact-ids: 13424016494\n'),
                ('dotnet', '--run-attempt ${{ github.run_attempt }}\n          --staging',
                 '--run-attempt 1\n          --staging'),
                ('dotnet', "      - name: Select each leg's latest-attempt artifact\n",
                 "      - name: Select each leg's latest-attempt artifact\n        if: always()\n")):
            with self.subTest(stack=stack, original=original):
                self.assertIn(original, self.workflow)
                invalid = self.workflow.replace(original, replacement)
                with self.assertRaises(AssertionError):
                    assert_rerun_safe_coverage_artifacts(self, invalid, stack)


if __name__ == '__main__':
    unittest.main()
