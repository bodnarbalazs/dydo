"""Coverage unions consume each platform leg's latest-execution artifact and fail closed on anything else."""
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
from coverage_artifacts import SelectionError, artifact_name, leg_attempts, select, stamp

RUN = 42038108870
COMMIT = '4620994056fbf022b0a10dcacdd07bd23a39fdc9'
WORKFLOW = Path(__file__).resolve().parents[3] / '.github' / 'workflows' / 'ci.yml'
STARTS = [{'run_attempt': 1, 'run_started_at': '2026-11-05T15:01:04Z'},
          {'run_attempt': 2, 'run_started_at': '2026-11-05T15:26:23Z'}]
MATRIX = {'linux': 'ubuntu-24.04, linux, .static-gates/python/bin/python',
          'windows': 'windows-latest, windows, .static-gates/python/Scripts/python.exe'}
# Run 42050664030's attempts/2/jobs record: rerun --failed reran only Windows, and the record
# carries the Linux legs into attempt 2 with their attempt-1 start times.
HOSTED_RERUN = [
    {'name': f'dotnet-coverage ({MATRIX["windows"]})', 'run_attempt': 2, 'conclusion': 'success',
     'started_at': '2026-11-05T15:26:27Z'},
    {'name': f'python-coverage ({MATRIX["windows"]})', 'run_attempt': 2, 'conclusion': 'success',
     'started_at': '2026-11-05T15:26:27Z'},
    {'name': f'python-coverage ({MATRIX["linux"]})', 'run_attempt': 2, 'conclusion': 'success',
     'started_at': '2026-11-05T15:01:07Z'},
    {'name': f'dotnet-coverage ({MATRIX["linux"]})', 'run_attempt': 2, 'conclusion': 'success',
     'started_at': '2026-11-05T15:01:40Z'},
    {'name': 'assurance-cancellation (windows-latest)', 'run_attempt': 2, 'conclusion': 'success',
     'started_at': '2026-11-05T15:01:08Z'},
    {'name': 'dotnet-coverage-union', 'run_attempt': 2, 'conclusion': 'success', 'started_at': '2026-11-05T15:32:02Z'},
    {'name': 'python-coverage-union', 'run_attempt': 2, 'conclusion': None, 'started_at': '2026-11-05T15:50:24Z'}]


def job(stack, platform, started, conclusion='success', attempt=2):
    return {'name': f'{stack}-coverage ({MATRIX[platform]})', 'run_attempt': attempt, 'conclusion': conclusion,
            'started_at': started}


class JobRecordTests(unittest.TestCase):
    def test_hosted_rerun_record_names_the_attempt_each_leg_executed_in(self):
        for stack in coverage_artifacts.STACKS:
            with self.subTest(stack=stack):
                self.assertEqual({'linux': 1, 'windows': 2}, leg_attempts(HOSTED_RERUN, STARTS, stack, 2))

    def test_a_leg_rerun_in_the_latest_attempt_executed_there(self):
        jobs = [job('python', 'linux', '2026-11-05T15:26:23Z'), job('python', 'windows', '2026-11-05T15:40:00Z')]
        self.assertEqual({'linux': 2, 'windows': 2}, leg_attempts(jobs, STARTS, 'python', 2))
        self.assertEqual({'linux': 1, 'windows': 1},
                         leg_attempts([job('python', p, '2026-11-05T15:02:00Z', attempt=1) for p in MATRIX],
                                      STARTS[:1], 'python', 1))

    def test_a_latest_leg_that_did_not_succeed_fails_closed(self):
        for conclusion in ('failure', 'cancelled', 'skipped', None):
            with self.subTest(conclusion=conclusion):
                jobs = [job('python', 'linux', '2026-11-05T15:01:07Z'),
                        job('python', 'windows', '2026-11-05T15:26:27Z', conclusion)]
                with self.assertRaisesRegex(SelectionError, rf"The latest python windows leg 'python-coverage "
                                                            rf"\(windows-latest, .*\)' concluded {conclusion}"):
                    leg_attempts(jobs, STARTS, 'python', 2)

    def test_a_missing_or_ambiguous_leg_record_fails_closed(self):
        with self.assertRaisesRegex(SelectionError, 'The job record has no latest dotnet linux leg'):
            leg_attempts(HOSTED_RERUN[:2], STARTS, 'dotnet', 2)
        for name in ('python-coverage (ubuntu-24.04, linux, windows)', 'python-coverage (macos-15)',
                     f'python-coverage ({MATRIX["windows"]})'):
            with self.subTest(name=name):
                with self.assertRaisesRegex(SelectionError, f'Job {re.escape(repr(name))} does not name one '
                                                            'new python leg platform'):
                    leg_attempts(HOSTED_RERUN + [{**HOSTED_RERUN[1], 'name': name}], STARTS, 'python', 2)

    def test_an_incomplete_attempt_record_fails_closed(self):
        for attempts in (STARTS[:1], STARTS[1:], STARTS + STARTS[1:], []):
            with self.subTest(attempts=attempts):
                with self.assertRaisesRegex(SelectionError, 'does not list attempts 1..2 once each'):
                    leg_attempts(HOSTED_RERUN, attempts, 'python', 2)

    def test_a_leg_that_started_before_the_run_fails_closed(self):
        jobs = [job('python', 'linux', '2026-11-05T15:01:03Z'), job('python', 'windows', '2026-11-05T15:26:27Z')]
        with self.assertRaisesRegex(SelectionError, 'The latest python linux leg started at 2026-11-05T15:01:03Z, '
                                                    'before attempt 1'):
            leg_attempts(jobs, STARTS, 'python', 2)


class SelectionTests(unittest.TestCase):
    def setUp(self):
        folder = tempfile.TemporaryDirectory()
        self.addCleanup(folder.cleanup)
        self.root = Path(folder.name)
        self.staging = self.root / 'coverage-legs'
        self.destinations = {platform: self.root / 'coverage-platforms' / platform
                             for platform in coverage_artifacts.PLATFORMS}

    def leg(self, platform, attempt, *, stack='python', commit=COMMIT, measured=COMMIT, name=None, report=True):
        folder = self.staging / (name or artifact_name(stack, platform, attempt))
        stamp(folder, stack, platform, RUN, attempt, commit)
        if report:
            (folder / 'adapters').mkdir()
            (folder / 'adapters' / f'{stack}-coverage.json').write_text(
                json.dumps({'candidate': {'commit': measured}, 'leg': f'{platform}-{attempt}'}), encoding='utf-8')
        return folder

    def select(self, stack='python', executions=None):
        return select(self.staging, stack, RUN, 2, COMMIT, executions or {'linux': 1, 'windows': 2},
                      self.destinations)

    def assertRefused(self, message, stack='python', executions=None):
        with self.assertRaisesRegex(SelectionError, message):
            self.select(stack, executions)
        self.assertFalse(any(path.exists() for path in self.destinations.values()))

    def moved(self, platform, stack='python'):
        report = self.destinations[platform] / 'adapters' / f'{stack}-coverage.json'
        return json.loads(report.read_text(encoding='utf-8'))['leg']

    def test_rerun_of_failed_leg_takes_each_legs_latest_execution(self):
        # The incident: attempt 1 Windows failed without an adapter report; rerun --failed reran only Windows.
        self.leg('linux', 1)
        self.leg('windows', 1, report=False)
        self.leg('windows', 2)
        receipt = self.select()
        self.assertEqual({'schema': 2, 'stack': 'python', 'run_id': RUN, 'run_attempt': 2, 'commit': COMMIT,
                          'legs': {'linux': {'artifact': 'python-coverage-linux-attempt-1', 'attempt': 1,
                                             'superseded': []},
                                   'windows': {'artifact': 'python-coverage-windows-attempt-2', 'attempt': 2,
                                               'superseded': ['python-coverage-windows-attempt-1']}}}, receipt)
        self.assertEqual(('linux-1', 'windows-2'), (self.moved('linux'), self.moved('windows')))
        self.assertEqual(['python-coverage-windows-attempt-1'], [p.name for p in self.staging.iterdir()])

    def test_the_executed_attempt_wins_whatever_the_staging_order(self):
        for attempt in (3, 1, 10, 2):
            self.leg('windows', attempt)
        self.leg('linux', 1)
        receipt = self.select(executions={'linux': 1, 'windows': 10})
        self.assertEqual(10, receipt['legs']['windows']['attempt'])
        self.assertEqual([artifact_name('python', 'windows', n) for n in (1, 2, 3)],
                         receipt['legs']['windows']['superseded'])

    def test_dotnet_union_selects_its_own_stack(self):
        self.leg('linux', 1, stack='dotnet')
        self.leg('windows', 2, stack='dotnet')
        receipt = self.select('dotnet')
        self.assertEqual('dotnet-coverage-windows-attempt-2', receipt['legs']['windows']['artifact'])
        self.assertEqual('windows-2', self.moved('windows', 'dotnet'))

    def test_missing_newest_upload_fails_closed_instead_of_taking_an_older_attempt(self):
        # Codex probe: Linux attempt 1 uploaded then failed; its attempt-2 rerun passed but no upload arrived.
        self.leg('linux', 1)
        self.leg('windows', 1)
        self.assertRefused(r"Missing python-coverage-linux-attempt-2, the artifact of the linux leg's latest "
                           r'execution \(staged linux attempts: \[1\]\)', executions={'linux': 2, 'windows': 1})

    def test_missing_leg_fails_closed(self):
        self.assertRefused(r'Missing python-coverage-linux-attempt-1, .* \(staged linux attempts: \[\]\)')
        self.leg('linux', 1)
        self.assertRefused('Missing python-coverage-windows-attempt-2')

    def test_artifact_newer_than_the_legs_latest_execution_fails_closed(self):
        self.leg('linux', 1)
        self.leg('windows', 2)
        self.leg('windows', 3)
        self.assertRefused(r"\['python-coverage-windows-attempt-3'\] are newer than the windows leg's latest "
                           'execution, attempt 2')

    def test_union_other_stack_or_bare_artifact_fails_closed(self):
        self.leg('linux', 1)
        self.leg('windows', 2)
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

    def test_matching_identities_of_the_wrong_candidate_fail_closed(self):
        # Codex probe: both identities name the same wrong commit, and the selection used to accept them.
        self.leg('linux', 1, commit='0' * 40)
        self.leg('windows', 2, commit='0' * 40)
        self.assertRefused(f'python-coverage-linux-attempt-1 identity .*0000000000.* does not match its leg, '
                           f'run {RUN}, attempt 1 and candidate {COMMIT}')

    def test_report_that_measured_another_candidate_fails_closed(self):
        self.leg('linux', 1)
        self.leg('windows', 2, measured='1a351bb1')
        self.assertRefused(f"python-coverage-windows-attempt-2 report measured '1a351bb1', not candidate {COMMIT}")

    def test_unreadable_or_shapeless_report_fails_closed(self):
        self.leg('linux', 1)
        windows = self.leg('windows', 2)
        report = windows / 'adapters' / 'python-coverage.json'
        for content in ('{}', '[]', '{"candidate": "x"}', '{"candidate": {}}'):
            with self.subTest(content=content):
                report.write_text(content, encoding='utf-8')
                self.assertRefused('python-coverage-windows-attempt-2 report measured None')
        report.unlink()
        self.assertRefused('python-coverage-windows-attempt-2 has no readable python-coverage.json')

    def test_identity_that_does_not_match_its_leg_fails_closed(self):
        self.leg('linux', 1)
        windows = self.leg('windows', 2)
        recorded = json.loads((windows / coverage_artifacts.IDENTITY).read_text(encoding='utf-8'))
        for field, value in (('run_attempt', 1), ('platform', 'linux'), ('stack', 'dotnet'), ('run_id', RUN + 1),
                             ('artifact', 'python-coverage-windows-attempt-1'), ('schema', 2), ('commit', ''),
                             ('commit', None), ('commit', '1a351bb1'), ('extra', True)):
            with self.subTest(field=field, value=value):
                (windows / coverage_artifacts.IDENTITY).write_text(json.dumps({**recorded, field: value}),
                                                                   encoding='utf-8')
                self.assertRefused('python-coverage-windows-attempt-2 identity .* does not match its leg, '
                                   f'run {RUN}, attempt 2 and candidate {COMMIT}')
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
        self.head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()

    def run_main(self, *argv):
        stdout, stderr = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            code = coverage_artifacts.main([str(value) for value in argv])
        return code, stdout.getvalue(), stderr.getvalue()

    def record(self, jobs, attempts=STARTS):
        for name, lines in (('jobs', jobs), ('attempts', attempts)):
            (self.root / f'{name}.jsonl').write_text(''.join(json.dumps(line) + '\n' for line in lines),
                                                     encoding='utf-8')

    def select_argv(self, commit):
        return ('select', '--stack', 'dotnet', '--run-id', RUN, '--run-attempt', 2, '--expected-commit', commit,
                '--jobs', self.root / 'jobs.jsonl', '--attempts', self.root / 'attempts.jsonl',
                '--staging', self.root / 'legs', '--linux', self.root / 'union' / 'linux',
                '--windows', self.root / 'union' / 'windows', '--receipt', self.root / 'union' / 'selection.json')

    def stamp_leg(self, platform, attempt):
        results = self.root / 'legs' / artifact_name('dotnet', platform, attempt)
        code, _, _ = self.run_main('stamp', '--stack', 'dotnet', '--platform', platform, '--run-id', RUN,
                                   '--run-attempt', attempt, '--results', results)
        self.assertEqual(0, code)
        (results / 'adapters').mkdir()
        (results / 'adapters' / 'dotnet-coverage.json').write_text(
            json.dumps({'candidate': {'commit': self.head}}), encoding='utf-8')

    def test_stamped_legs_select_by_the_job_record_and_refusals_exit_two(self):
        for platform, attempt in (('linux', 1), ('windows', 1), ('windows', 2)):
            self.stamp_leg(platform, attempt)
        stamped = json.loads((self.root / 'legs' / 'dotnet-coverage-windows-attempt-2'
                              / coverage_artifacts.IDENTITY).read_text(encoding='utf-8'))
        self.assertEqual(coverage_artifacts.identity('dotnet', 'windows', RUN, 2, self.head), stamped)

        self.record(HOSTED_RERUN)
        code, _, stderr = self.run_main(*self.select_argv('0' * 40))
        self.assertEqual(2, code)
        self.assertIn('SelectionError: dotnet-coverage-linux-attempt-1 identity', stderr)
        self.assertFalse((self.root / 'union').exists())

        self.record([job('dotnet', 'linux', '2026-11-05T15:27:00Z'), *HOSTED_RERUN[:1]])
        code, _, stderr = self.run_main(*self.select_argv(self.head))
        self.assertEqual(2, code)
        self.assertIn('Missing dotnet-coverage-linux-attempt-2', stderr)

        self.record([{'name': f'dotnet-coverage ({MATRIX["linux"]})'}])
        code, _, stderr = self.run_main(*self.select_argv(self.head))
        self.assertEqual((2, "KeyError: 'conclusion'\n"), (code, stderr))

        self.record(HOSTED_RERUN)
        code, stdout, _ = self.run_main(*self.select_argv(self.head))
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
    return {key: _mapping(value[1:], depth + 2) if key in ('with', 'env', 'permissions')
            else ' '.join(part.strip() for part in value if part.strip() not in ('', '>-', '|'))
            for key, value in fields.items()}


def _steps(job):
    blocks = re.split(r'^      - ', job.split('\n    steps:\n', 1)[1], flags=re.M)[1:]
    return [_mapping(('        ' + block).splitlines(), 8) for block in blocks]


def _header(job):
    return _mapping([line for line in job.split('\n    steps:\n', 1)[0].splitlines() if line.strip()], 4)


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
# One tree per run: the merge ref on a pull request, whose ci.yml is running, and the pushed commit on a push.
RUN_COMMIT = '${{ github.sha }}'
HEAD_REF = '${{ github.event.pull_request.head.sha || github.sha }}'
CHECKOUT_JOBS = ('python-coverage', 'python-coverage-union', 'dotnet-coverage', 'dotnet-coverage-union',
                 'guard-windows', 'assurance-cancellation', 'viewer', 'build', 'full-stack')
DOTNET_PREFLIGHT = 'Preflight candidate, selection pins and nonzero test discovery'
RESULTS = 'DynaDocs.Tests/coverage/results'
PLATFORMS_DIR = f'{RESULTS}/coverage-platforms'
RECORD = "Record each leg's latest execution from the run's job record"
SELECT = "Select each leg's latest-execution artifact"


def assert_leg_uploads_stamped_attempt_artifact(test, workflow, stack):
    leg_job, _, upload, stamp_step, _, _ = LEGS[stack]
    leg = _steps(_job(workflow, leg_job))
    uploads = _uses(leg, 'actions/upload-artifact@')
    test.assertEqual([_named(leg, upload)], uploads)
    test.assertEqual(f'{stack}-coverage-${{{{ matrix.platform }}}}-attempt-${{{{ github.run_attempt }}}}',
                     leg[uploads[0]]['with']['name'])
    test.assertEqual(RESULTS, leg[uploads[0]]['with']['path'])
    test.assertEqual(_named(leg, stamp_step) + 1, uploads[0])
    for step in (leg[uploads[0]], leg[uploads[0] - 1]):
        test.assertEqual('always()', step.get('if'))
    test.assertEqual(f'python DynaDocs.Tests/coverage/coverage_artifacts.py stamp --stack {stack} '
                     '--platform ${{ matrix.platform }} --run-id ${{ github.run_id }} '
                     f'--run-attempt ${{{{ github.run_attempt }}}} --results {RESULTS}', leg[uploads[0] - 1]['run'])


def assert_union_downloads_every_attempt(test, union, stack):
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
    return downloads[0]


def assert_union_records_the_job_record(test, header, step):
    test.assertEqual({'actions': 'read', 'contents': 'read'}, header.get('permissions'))
    test.assertNotIn('if', step)
    test.assertEqual('bash', step.get('shell'))
    test.assertEqual({'GH_TOKEN': '${{ github.token }}',
                      'RUN_API': 'repos/${{ github.repository }}/actions/runs/${{ github.run_id }}'}, step['env'])
    test.assertEqual(
        'gh api --paginate "$RUN_API/attempts/${{ github.run_attempt }}/jobs?per_page=100" --jq \'.jobs[]\' > '
        f'{PLATFORMS_DIR}/run-jobs.jsonl for attempt in $(seq ${{{{ github.run_attempt }}}}); do '
        'gh api "$RUN_API/attempts/$attempt" --jq \'{run_attempt, run_started_at}\' done > '
        f'{PLATFORMS_DIR}/run-attempts.jsonl', step['run'])


def assert_rerun_safe_coverage_artifacts(test, workflow, stack):
    _, union_job, _, _, verify, folder = LEGS[stack]
    assert_leg_uploads_stamped_attempt_artifact(test, workflow, stack)
    union = _steps(_job(workflow, union_job))
    download = assert_union_downloads_every_attempt(test, union, stack)
    record, selection = _named(union, RECORD), _named(union, SELECT)
    test.assertTrue(download < _named(union, 'Require both native jobs to pass') < record < selection
                    < _named(union, verify))
    assert_union_records_the_job_record(test, _header(_job(workflow, union_job)), union[record])
    test.assertNotIn('if', union[selection])
    run = union[selection]['run']
    test.assertTrue(run.startswith('python DynaDocs.Tests/coverage/coverage_artifacts.py select '))
    test.assertIn(f'--run-id ${{{{ github.run_id }}}} --run-attempt ${{{{ github.run_attempt }}}} '
                  f'--expected-commit {RUN_COMMIT} --jobs', run)
    arguments = _arguments(run)
    test.assertEqual((stack, f'{RESULTS}/coverage-legs', f'{PLATFORMS_DIR}/artifact-selection.json',
                      f'{PLATFORMS_DIR}/run-jobs.jsonl', f'{PLATFORMS_DIR}/run-attempts.jsonl'),
                     tuple(arguments[key] for key in ('stack', 'staging', 'receipt', 'jobs', 'attempts')))
    verified = _arguments(union[_named(union, verify)]['run'])
    for platform in coverage_artifacts.PLATFORMS:
        destination = f'{PLATFORMS_DIR}/{folder.format(platform=platform)}'
        test.assertEqual(destination, arguments[platform])
        test.assertEqual(f'{destination}/adapters/{stack}-coverage.json', verified[platform])


def _jobs(workflow):
    section = workflow.split('\njobs:\n', 1)[1]
    return {name: _job(section, name) for name in re.findall(r'^  ([\w-]+):', section, re.M)}


def assert_one_tree_per_run(test, workflow):
    """Every checkout is the run's own commit, and no step moves the tree to another ref."""
    checked = set()
    for name, job in _jobs(workflow).items():
        steps = _steps(job)
        for index in _uses(steps, 'actions/checkout@'):
            test.assertEqual(RUN_COMMIT, steps[index].get('with', {}).get('ref'), name)
            checked.add(name)
        for step in steps:
            test.assertIsNone(re.search(r'\bgit\s+(checkout|switch|reset)\b', step.get('run', '')), name)
    test.assertLessEqual(set(CHECKOUT_JOBS), checked)


def assert_leg_and_union_measure_one_commit(test, workflow, stack):
    leg_job, union_job = LEGS[stack][:2]
    union = _steps(_job(workflow, union_job))
    expected = re.search(r'--expected-commit (\$\{\{.*?\}\}|\S+)', union[_named(union, SELECT)]['run'])[1]
    test.assertEqual(RUN_COMMIT, expected)
    for job in (leg_job, union_job):
        steps = _steps(_job(workflow, job))
        checkouts = _uses(steps, 'actions/checkout@')
        test.assertEqual(1, len(checkouts), job)
        test.assertEqual(expected, steps[checkouts[0]].get('with', {}).get('ref'), job)


def assert_dotnet_preflight_asserts_the_run_commit(test, workflow):
    steps = _steps(_job(workflow, 'dotnet-coverage'))
    preflight = steps[_named(steps, DOTNET_PREFLIGHT)]
    test.assertEqual('${{ github.event.pull_request.head.sha }}', preflight['env']['PR_HEAD_SHA'])
    test.assertEqual(RUN_COMMIT, preflight['env']['MERGE_REF_SHA'])
    run = preflight['run']
    test.assertIn("assert candidate == os.environ['MERGE_REF_SHA'], candidate", run)
    test.assertEqual(1, run.count('assert candidate =='))
    test.assertIn("'pr_head_sha': os.environ['PR_HEAD_SHA'], 'merge_ref_sha': os.environ['MERGE_REF_SHA']", run)


def _with_job(workflow, name, edit):
    job = _jobs(workflow)[name]
    edited = edit(job)
    assert edited != job, name
    return workflow.replace(job, edited, 1)


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.workflow = WORKFLOW.read_text(encoding='utf-8').replace('\r\n', '\n')

    def test_both_unions_select_each_legs_latest_execution_for_their_candidate(self):
        for stack in coverage_artifacts.STACKS:
            with self.subTest(stack=stack):
                assert_rerun_safe_coverage_artifacts(self, self.workflow, stack)

    def test_each_legs_checkout_is_the_unions_expected_candidate(self):
        for stack in coverage_artifacts.STACKS:
            with self.subTest(stack=stack):
                assert_leg_and_union_measure_one_commit(self, self.workflow, stack)

    def test_every_job_checks_out_the_runs_own_commit(self):
        assert_one_tree_per_run(self, self.workflow)

    def test_dotnet_preflight_asserts_the_run_commit_and_records_both_shas(self):
        assert_dotnet_preflight_asserts_the_run_commit(self, self.workflow)

    def test_a_job_checking_out_another_ref_is_rejected(self):
        pinned = f'          ref: {RUN_COMMIT}\n'
        mutations = {
            'head': lambda job: job.replace(pinned, f'          ref: {HEAD_REF}\n'),
            'head-only': lambda job: job.replace(pinned, '          ref: ${{ github.event.pull_request.head.sha }}\n'),
            # The default ref: the checkout loses its ref, and its with: when nothing else is left in it.
            'default': lambda job: re.sub(r'^        with:\n(?! {10})', '', job.replace(pinned, ''), flags=re.M)}
        for name in CHECKOUT_JOBS:
            for label, edit in mutations.items():
                with self.subTest(job=name, mutation=label):
                    invalid = _with_job(self.workflow, name, edit)
                    with self.assertRaises(AssertionError):
                        assert_one_tree_per_run(self, invalid)
        moved = _with_job(self.workflow, 'guard-windows', lambda job: job.replace(
            '      - name: Setup .NET\n',
            '      - name: Move to the head\n        run: git checkout ${{ github.event.pull_request.head.sha }}\n\n'
            '      - name: Setup .NET\n', 1))
        with self.assertRaises(AssertionError):
            assert_one_tree_per_run(self, moved)

    def test_a_leg_and_union_that_disagree_are_rejected(self):
        for stack in coverage_artifacts.STACKS:
            leg_job, union_job = LEGS[stack][:2]
            for name, original in ((leg_job, 'ref: '), (union_job, 'ref: '), (union_job, '--expected-commit ')):
                with self.subTest(stack=stack, job=name, field=original):
                    invalid = _with_job(self.workflow, name, lambda job: job.replace(
                        original + RUN_COMMIT, original + HEAD_REF, 1))
                    with self.assertRaises(AssertionError):
                        assert_leg_and_union_measure_one_commit(self, invalid, stack)

    def test_a_dotnet_preflight_that_accepts_the_head_or_drops_a_sha_is_rejected(self):
        assertion = "assert candidate == os.environ['MERGE_REF_SHA'], candidate"
        for original, replacement in (
                (assertion,
                 "assert candidate == (os.environ['PR_HEAD_SHA'] or os.environ['MERGE_REF_SHA']), candidate"),
                (assertion, 'pass'),
                ("'pr_head_sha': os.environ['PR_HEAD_SHA'],\n", '\n'),
                (f'MERGE_REF_SHA: {RUN_COMMIT}\n', 'MERGE_REF_SHA: ${{ github.event.pull_request.head.sha }}\n')):
            with self.subTest(original=original, replacement=replacement):
                invalid = _with_job(self.workflow, 'dotnet-coverage', lambda job: job.replace(original, replacement, 1))
                with self.assertRaises(AssertionError):
                    assert_dotnet_preflight_asserts_the_run_commit(self, invalid)

    def test_stale_prone_or_unanchored_selection_is_rejected(self):
        for stack, original, replacement in (
                ('python', 'pattern: python-coverage-*-attempt-*', 'pattern: python-coverage-*'),
                ('python', '-attempt-${{ github.run_attempt }}\n', '\n'),
                ('python', '--platform ${{ matrix.platform }} --run-id ${{ github.run_id }} --run-attempt '
                 '${{ github.run_attempt }}\n          --results',
                 '--platform ${{ matrix.platform }} --run-id ${{ github.run_id }} --run-attempt '
                 '${{ github.run_id }}\n          --results'),
                ('dotnet', '--platform ${{ matrix.platform }} --run-id ${{ github.run_id }}',
                 '--platform linux --run-id ${{ github.run_id }}'),
                ('python', '          merge-multiple: false\n', '          merge-multiple: true\n'),
                ('dotnet', 'pattern: dotnet-coverage-*-attempt-*', 'name: dotnet-coverage-windows'),
                ('dotnet', 'pattern: dotnet-coverage-*-attempt-*\n',
                 'pattern: dotnet-coverage-*-attempt-*\n          artifact-ids: 13424016494\n'),
                ('dotnet', '--run-attempt ${{ github.run_attempt }}\n          --expected-commit',
                 '--run-attempt 1\n          --expected-commit'),
                ('dotnet', f"      - name: {SELECT}\n", f"      - name: {SELECT}\n        if: always()\n"),
                ('python', '--expected-commit ${{ github.sha }}', '--expected-commit 0000000'),
                ('dotnet', f'--expected-commit {RUN_COMMIT}\n          --jobs',
                 f'--expected-commit {HEAD_REF}\n          --jobs'),
                ('dotnet', '/attempts/${{ github.run_attempt }}/jobs?', '/jobs?filter=all&'),
                ('python', '    permissions:\n      actions: read\n      contents: read\n    steps:',
                 '    steps:'),
                ('dotnet', '    permissions:\n      actions: read\n      contents: read\n    steps:',
                 '    permissions:\n      actions: read\n    steps:')):
            with self.subTest(stack=stack, original=original):
                self.assertIn(original, self.workflow)
                invalid = self.workflow.replace(original, replacement)
                with self.assertRaises(AssertionError):
                    assert_rerun_safe_coverage_artifacts(self, invalid, stack)


if __name__ == '__main__':
    unittest.main()
