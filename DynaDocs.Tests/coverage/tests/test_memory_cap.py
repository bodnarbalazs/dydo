"""The facade's shared-host memory cap: enforced, inherited and unavailable, and the cap-hit row."""
import contextlib
import io
import itertools
import json
import os
import runpy
import shutil
import signal
import subprocess
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[3]
COVERAGE = ROOT / 'DynaDocs.Tests/coverage'
RUNNER = COVERAGE / 'gap_check.py'
PORTABLE = ROOT / 'dydo/reference/gap-check.example.py'
GIB = 1024 ** 3
LEAF = 'user.slice/user-1000.slice/user@1000.service/app.slice/run-p1.scope'
FINDING = 'a finding; do not raise the cap'


class Exec(BaseException):
    """os.execv stand-in: the real one never returns, so the run under test stops here."""


class FakeHost:
    """A cgroup v2 tree, /proc/self/cgroup, PATH and the systemd tools, all scripted."""

    def __init__(self, directory, leaf=LEAF):
        self.cgroups = directory / 'cgroup'
        self.self_cgroup = directory / 'self-cgroup'
        self.self_cgroup.write_text(f'0::/{leaf}\n', encoding='ascii')
        self.leaf = self.cgroups / leaf
        self.leaf.mkdir(parents=True)
        for level in [self.leaf, *self.leaf.parents]:
            if level == self.cgroups:
                break
            self.limit(level.relative_to(self.cgroups), 'max', 'max')
        self.tools = {'systemctl', 'systemd-run'}
        self.policy, self.probe_exit, self.probe_error, self.calls = 'continue', 0, None, []

    def limit(self, relative, memory, swap, kills=0):
        directory = self.cgroups / relative
        (directory / 'memory.max').write_text(f'{memory}\n', encoding='ascii')
        (directory / 'memory.swap.max').write_text(f'{swap}\n', encoding='ascii')
        (directory / 'memory.events').write_text(f'oom 0\noom_kill {kills}\n', encoding='ascii')
        return directory

    def which(self, name, path=None):
        return f'/usr/bin/{name}' if name in self.tools else None

    def run(self, command, **_options):
        self.calls.append(command)
        if Path(command[0]).name == 'systemctl':
            return subprocess.CompletedProcess(command, 0, stdout=f'{self.policy}\n', stderr='')
        if self.probe_error:
            raise self.probe_error
        stderr = '' if self.probe_exit == 0 else 'Failed to connect to user scope bus via local transport\n'
        return subprocess.CompletedProcess(command, self.probe_exit, stdout='', stderr=stderr)


class MemoryCapTests(unittest.TestCase):
    runner = RUNNER

    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix='dydo-memory-cap-')
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        # The runner's live globals: run_path returns a copy, and a patch of the copy reaches nothing.
        self.namespace = runpy.run_path(str(self.runner))['main'].__globals__
        self.host = FakeHost(self.directory)

    def cap(self, environment=None, platform='linux', orig_argv=None):
        """The runner's memory_cap() on the fake host: its cap, or the (path, argv, environment) it
        execs, and its stderr. The environment memory_cap leaves behind is kept in self.left."""
        executed = []

        def execv(path, argv):
            executed.append((path, argv, dict(os.environ)))
            raise Exec

        tools = types.SimpleNamespace(run=self.host.run, DEVNULL=subprocess.DEVNULL,
                                      TimeoutExpired=subprocess.TimeoutExpired)
        stderr = io.StringIO()
        variables = {key: value for key, value in os.environ.items()
                     if key not in ('DYDO_MEMORY_MAX', 'DYDO_MEMORY_CAP_SCOPE')}
        with (mock.patch.dict(self.namespace, {'CGROUP_ROOT': self.host.cgroups,
                                               'SELF_CGROUP': self.host.self_cgroup,
                                               'subprocess': tools}),
              mock.patch.object(shutil, 'which', self.host.which),
              mock.patch.dict(os.environ, {**variables, **(environment or {})}, clear=True),
              mock.patch.object(os, 'execv', execv),
              mock.patch.object(sys, 'platform', platform),
              mock.patch.object(sys, 'orig_argv', orig_argv or [sys.executable, '-u', 'gap_check.py', 'all']),
              contextlib.redirect_stderr(stderr)):
            try:
                outcome = self.namespace['memory_cap']()
            except Exec:
                outcome = executed[0]
            finally:
                self.left = dict(os.environ)
        return outcome, stderr.getvalue()

    def assert_record(self, cap, state, limit):
        self.assertEqual({'state', 'limit', 'mechanism', 'reason'}, set(cap.record))
        self.assertEqual((state, limit), (cap.record['state'], cap.record['limit']))
        self.assertIsInstance(cap.record['reason'], str)

    # Enforced

    def test_enforced_scopes_and_probes_join_the_nearest_user_manager_slice(self):
        for suffix, expected in (
                ('app.slice/app-t3code.slice/app-t3code-agents.slice/app-t3code-agents-session.slice/run.scope',
                 'app-t3code-agents-session.slice'),
                ('background.slice/background-worker.slice/service.service/child.scope',
                 'background-worker.slice')):
            with self.subTest(suffix=suffix):
                self.host.self_cgroup.write_text(f'0::/user.slice/user-1000.slice/user@1000.service/{suffix}\n')
                (_, executed, _), _ = self.cap()
                self.assertIn(f'--slice={expected}', executed)
                self.assertIn(f'--slice={expected}', self.host.calls[-1])

    def test_enforced_scopes_without_a_user_manager_slice_keep_the_default(self):
        for relative in ('user.slice/user-1000.slice/user@1000.service/app.slice/run.scope',
                         'user.slice/user-1000.slice/user@1000.service/run.scope',
                         'system.slice/agent.service/private.slice/run.scope', ''):
            with self.subTest(relative=relative):
                self.host.self_cgroup.write_text(f'0::/{relative}\n')
                (_, executed, _), _ = self.cap()
                self.assertFalse(any(arg.startswith('--slice=') for arg in executed))

    def test_enforced_record_names_the_slice_used(self):
        leaf = 'user.slice/user-1000.slice/user@1000.service/app.slice/app-agents.slice/run.scope'
        (self.directory / 'nested').mkdir()
        self.host = FakeHost(self.directory / 'nested', leaf)
        self.host.limit(leaf, GIB, 0)
        cap, _ = self.cap({'DYDO_MEMORY_CAP_SCOPE': '1'})
        self.assertIn('slice=app-agents.slice', cap.record['reason'])

    def test_child_environment_defaults_disable_dotnet_servers_on_every_cap_path(self):
        names = ('MSBUILDDISABLENODEREUSE', 'DOTNET_CLI_USE_MSBUILD_SERVER', 'UseSharedCompilation')
        names = tuple(name.upper() if sys.platform == 'win32' else name for name in names)
        clean = {key: value for key, value in os.environ.items() if key not in names}
        for mode in ('enforced', 'rerun', 'inherited', 'unavailable'):
            with self.subTest(mode=mode), mock.patch.dict(os.environ, clean, clear=True):
                self.host.limit(LEAF, GIB if mode in ('rerun', 'inherited') else 'max', 0)
                self.cap({'DYDO_MEMORY_CAP_SCOPE': '1'} if mode == 'rerun' else None,
                         platform='darwin' if mode == 'unavailable' else 'linux')
                self.assertEqual(['1', '0', 'false'], [self.left.get(name) for name in names])

    def test_child_environment_preserves_each_caller_dotnet_setting(self):
        names = ('MSBUILDDISABLENODEREUSE', 'DOTNET_CLI_USE_MSBUILD_SERVER', 'UseSharedCompilation')
        names = tuple(name.upper() if sys.platform == 'win32' else name for name in names)
        clean = {key: value for key, value in os.environ.items() if key not in names}
        for name in names:
            for value in ('caller-value', ''):
                with self.subTest(name=name, value=value), mock.patch.dict(os.environ, clean, clear=True):
                    self.cap({name: value})
                    expected = dict(zip(names, ('1', '0', 'false')))
                    expected[name] = value
                    self.assertEqual(expected, {key: self.left.get(key) for key in names})

    def test_windows_child_environment_preserves_caller_casing_and_defaults_exactly_once(self):
        for name, default in (('MSBUILDDISABLENODEREUSE', '1'), ('DOTNET_CLI_USE_MSBUILD_SERVER', '0'),
                              ('UseSharedCompilation', 'false')):
            for key, value in itertools.product((name.upper(), name.lower(), name), (None, 'true', '')):
                environment = {} if value is None else {key: value}
                with self.subTest(key=key, value=value), mock.patch.object(os, 'environ', environment):
                    self.cap(platform='win32')
                    matches = [setting for variable, setting in self.left.items() if variable.upper() == name.upper()]
                    self.assertEqual([default if value is None else value], matches)
                    if value is not None:
                        self.assertEqual(value, self.left[key])

    def test_posix_child_environment_keeps_names_case_sensitive(self):
        for platform in ('linux', 'darwin'):
            with self.subTest(platform=platform), mock.patch.object(os, 'environ', {'USESHAREDCOMPILATION': 'true'}):
                self.cap(platform=platform)
                self.assertEqual('true', self.left['USESHAREDCOMPILATION'])
                self.assertEqual('false', self.left['UseSharedCompilation'])

    def test_an_uncapped_linux_operation_reruns_once_under_a_capped_systemd_scope(self):
        argv = [sys.executable, '-u', 'run_gap_check.py', 'test', '--stack', 'x', '--', 'a$(b', '$HOME', '$$x']
        (path, executed, environment), stderr = self.cap(orig_argv=argv)
        wrap = ['/usr/bin/systemd-run', '--user', '--scope', '--quiet', '-p', f'MemoryMax={8 * GIB}',
                '-p', 'MemorySwapMax=0', '-p', 'OOMPolicy=continue', '--expand-environment=no', '--']
        self.assertEqual('/usr/bin/systemd-run', path)
        self.assertEqual([*wrap, sys.executable, *argv[1:]], executed)
        self.assertEqual([[*wrap, 'true']], self.host.calls)
        self.assertEqual('1', environment['DYDO_MEMORY_CAP_SCOPE'])
        self.assertEqual('', stderr)

    def test_the_rerun_inside_its_scope_records_the_enforced_cap_and_never_wraps_again(self):
        self.host.limit(LEAF, 268435456, 0)
        cap, stderr = self.cap({'DYDO_MEMORY_CAP_SCOPE': '1', 'DYDO_MEMORY_MAX': '256M'})
        self.assert_record(cap, 'enforced', 268435456)
        self.assertEqual(self.host.leaf, cap.cgroup)
        self.assertIn('systemd-run', cap.record['mechanism'])
        self.assertEqual([], self.host.calls)
        self.assertEqual('', stderr)
        self.assertNotIn('DYDO_MEMORY_CAP_SCOPE', self.left)

    def test_a_rerun_whose_scope_shows_no_cap_is_unavailable_and_never_wraps_twice(self):
        for cgroup_line in (f'0::/{LEAF}\n', ''):
            with self.subTest(cgroup_line=cgroup_line):
                self.host.self_cgroup.write_text(cgroup_line, encoding='ascii')
                cap, stderr = self.cap({'DYDO_MEMORY_CAP_SCOPE': '1'})
                self.assert_record(cap, 'unavailable', None)
                self.assertIsNone(cap.cgroup)
                self.assertEqual([], self.host.calls)
                self.assertEqual(1, stderr.count('memory cap unavailable'))

    def test_an_outer_limit_above_the_target_is_reenforced_at_the_target(self):
        self.host.limit('user.slice', 16 * GIB, 0)
        (_, executed, _), _ = self.cap()
        self.assertIn(f'MemoryMax={8 * GIB}', executed)

    def test_an_outer_limit_with_swap_on_is_reenforced_at_that_tighter_limit(self):
        self.host.limit(Path(LEAF).parent, 4 * GIB, 'max')
        (_, executed, _), _ = self.cap()
        self.assertIn(f'MemoryMax={4 * GIB}', executed)
        self.assertNotIn('systemctl', [Path(call[0]).name for call in self.host.calls])

    def test_an_outer_scope_whose_oom_policy_is_not_continue_is_reenforced_at_its_limit(self):
        for policy, tools in (('stop', {'systemctl', 'systemd-run'}), ('', {'systemctl', 'systemd-run'}),
                              ('continue', {'systemd-run'})):
            with self.subTest(policy=policy, tools=sorted(tools)):
                self.host.limit(LEAF, 2 * GIB, 0)
                self.host.policy, self.host.tools, self.host.calls = policy, tools, []
                (_, executed, _), _ = self.cap()
                self.assertIn(f'MemoryMax={2 * GIB}', executed)

    def test_an_unanswered_oom_policy_query_counts_as_stop(self):
        for failure in (OSError('no systemctl'), subprocess.TimeoutExpired('systemctl', 30), 1):
            with self.subTest(failure=failure):
                self.host.limit(LEAF, 2 * GIB, 0)
                answer = self.host.run

                def run(command, **options):
                    if Path(command[0]).name != 'systemctl':
                        return answer(command, **options)
                    if isinstance(failure, int):
                        return subprocess.CompletedProcess(command, failure, stdout='continue\n', stderr='')
                    raise failure
                with mock.patch.object(self.host, 'run', run):
                    (_, executed, _), _ = self.cap()
                self.assertIn(f'MemoryMax={2 * GIB}', executed)

    def test_without_cgroup_v2_an_operation_is_still_enforced_at_the_target(self):
        self.host.self_cgroup.write_text('1:name=systemd:/\n', encoding='ascii')
        (_, executed, _), _ = self.cap()
        self.assertIn(f'MemoryMax={8 * GIB}', executed)
        self.host.self_cgroup.unlink()
        (_, executed, _), _ = self.cap()
        self.assertIn(f'MemoryMax={8 * GIB}', executed)

    # Inherited

    def test_an_outer_scope_with_swap_off_and_oom_policy_continue_is_inherited(self):
        outer = self.host.limit(LEAF, 4 * GIB, 0)
        cap, stderr = self.cap()
        self.assert_record(cap, 'inherited', 4 * GIB)
        self.assertEqual(outer, cap.cgroup)
        self.assertEqual([['/usr/bin/systemctl', '--user', 'show', '-p', 'OOMPolicy', '--value', 'run-p1.scope']],
                         self.host.calls)
        self.assertEqual('', stderr)

    def test_the_tightest_ancestor_decides_inheritance(self):
        self.host.limit(LEAF, 6 * GIB, 0)
        self.host.limit('user.slice', 5 * GIB, 0)
        cap, _ = self.cap()
        self.assert_record(cap, 'inherited', 5 * GIB)
        self.assertEqual(self.host.cgroups / 'user.slice', cap.cgroup)

    # Unavailable

    def test_without_systemd_run_on_path_the_cap_is_unavailable_with_one_warning(self):
        self.host.tools = {'systemctl'}
        cap, stderr = self.cap()
        self.assert_record(cap, 'unavailable', None)
        self.assertIn('systemd-run', cap.record['reason'])
        self.assertEqual(1, stderr.count('\n'))
        self.assertIn('memory cap unavailable', stderr)

    def test_a_failing_systemd_run_probe_leaves_the_cap_unavailable(self):
        for exit_code, error in ((1, None), (0, OSError('no bus')), (0, subprocess.TimeoutExpired('systemd-run', 30))):
            with self.subTest(exit_code=exit_code, error=error):
                self.host.probe_exit, self.host.probe_error = exit_code, error
                cap, stderr = self.cap()
                self.assert_record(cap, 'unavailable', None)
                self.assertIn('systemd-run', cap.record['reason'])
                self.assertEqual(1, stderr.count('memory cap unavailable'))

    def test_a_probe_failure_names_the_systemd_run_diagnostic(self):
        self.host.probe_exit = 1
        cap, _ = self.cap()
        self.assertIn('exit 1', cap.record['reason'])
        self.assertIn('Failed to connect', cap.record['reason'])

    def test_windows_and_macos_record_the_cap_unavailable_with_a_platform_reason(self):
        for platform in ('win32', 'darwin'):
            with self.subTest(platform=platform):
                cap, stderr = self.cap(platform=platform)
                self.assert_record(cap, 'unavailable', None)
                self.assertIn(platform, cap.record['reason'])
                self.assertIn('1800 s', cap.record['reason'])
                self.assertEqual(1, stderr.count('memory cap unavailable'))
                self.assertEqual([], self.host.calls)

    # DYDO_MEMORY_MAX

    def test_dydo_memory_max_may_lower_the_cap(self):
        for value, expected in (('256M', 256 * 1024 ** 2), ('1.5G', 3 * GIB // 2), ('300000000', 300000000),
                                ('8G', 8 * GIB), ('64K', 65536)):
            with self.subTest(value=value):
                (_, executed, _), _ = self.cap({'DYDO_MEMORY_MAX': value})
                self.assertIn(f'MemoryMax={expected}', executed)

    def test_dydo_memory_max_above_the_default_is_refused(self):
        for value in ('9G', '1T', '8589934593'):
            with self.subTest(value=value):
                with self.assertRaises(self.namespace['ContractError']) as refused:
                    self.cap({'DYDO_MEMORY_MAX': value})
                self.assertIn('a cap hit is a finding; raising the cap is not allowed', str(refused.exception))
                self.assertEqual([], self.host.calls)

    def test_a_malformed_dydo_memory_max_is_refused(self):
        for value in ('', 'lots', '50%', 'infinity', '0', '-1G', '1g', '1.G', ' 1G'):
            with self.subTest(value=value):
                with self.assertRaises(self.namespace['ContractError']) as refused:
                    self.cap({'DYDO_MEMORY_MAX': value})
                self.assertIn('DYDO_MEMORY_MAX', str(refused.exception))

    # The cap-hit row

    def stack(self, capability, code, artifact=None, name='hog'):
        caps = {item: {'state': 'unavailable', 'reason': 'fixture'} for item in ('test', 'static', 'coverage', 'mutation')}
        caps[capability] = {'state': 'configured', 'command': {'kind': 'current-python', 'argv': ['-c', code]},
                            'artifacts': [{'path': artifact, 'required': True}] if artifact else []}
        return {'name': name, 'kind': 'fixture', 'cwd': '.', 'capabilities': caps,
                'isolation': {'requirement': 'in-place', 'evidence': {'state': 'verified', 'kind': 'direct'}}}

    def rows(self, stack, capabilities, kills=1, cap_limit=256 * 1024 ** 2):
        """execute_rows under an enforced cap whose memory.events the rows' code may rewrite."""
        cgroup = self.host.limit(LEAF, cap_limit, 0)
        events = cgroup / 'memory.events'
        cap = self.namespace['MemoryCap']('enforced', cap_limit, 'systemd-run --user --scope', 'fixture', cgroup)
        stack['capabilities'] = json.loads(json.dumps(stack['capabilities']).replace('EVENTS', events.as_posix())
                                           .replace('KILLS', str(kills)))
        stdout, stderr = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            rows = self.namespace['execute_rows']([stack], capabilities, self.directory.resolve(), None, [], cap)
        return rows, stdout.getvalue(), stderr.getvalue()

    def test_a_row_that_raises_the_oom_kill_count_fails_as_a_cap_hit(self):
        hit = "from pathlib import Path; Path('EVENTS').write_text('oom_kill KILLS\\n'); raise SystemExit(EXIT)"
        for capability, code, artifact in (('test', hit.replace('EXIT', '0'), None),
                                           ('test', hit.replace('EXIT', '137'), None),
                                           ('static', hit.replace('EXIT', '0'), 'missing.json'),
                                           ('static', hit.replace('EXIT', '130'), 'missing.json')):
            with self.subTest(capability=capability, code=code):
                rows, stdout, stderr = self.rows(self.stack(capability, code, artifact), (capability,))
                row = rows[0]
                reason = f'memory cap hit (MemoryMax=256M): {FINDING}'
                self.assertEqual(('failed', 1, True, reason),
                                 (row['state'], row['resultExit'], row['memoryCapHit'], row['reason']))
                self.assertIn(f'hog {capability}: {reason}', stderr)
                self.assertIn('FAILED', stdout)

    def test_a_row_under_an_unchanged_oom_kill_count_keeps_its_outcome(self):
        code = "from pathlib import Path; Path('EVENTS').write_text('oom_kill KILLS\\n')"
        rows, _, stderr = self.rows(self.stack('test', code), ('test',), kills=0)
        self.assertEqual('passed', rows[0]['state'])
        self.assertNotIn('memoryCapHit', rows[0])
        self.assertEqual('', stderr)

    def test_an_unreadable_oom_kill_count_is_no_cap_hit(self):
        for events in ('', 'oom_kill many\n'):
            with self.subTest(events=events):
                code = f"from pathlib import Path; Path('EVENTS').write_text({events!r})"
                rows, _, _ = self.rows(self.stack('test', code), ('test',))
                self.assertEqual('passed', rows[0]['state'])
        rows, _, _ = self.rows(self.stack('test', "from pathlib import Path; Path('EVENTS').unlink()"), ('test',))
        self.assertEqual('passed', rows[0]['state'])

    def test_a_suite_verdict_derived_from_a_cap_hit_coverage_row_fails_as_the_cap_hit(self):
        stack = self.stack('coverage', "from pathlib import Path; Path('EVENTS').write_text('oom_kill KILLS\\n')",
                           'coverage.json')
        stack['capabilities']['test'] = {'state': 'configured', 'artifacts': [],
                                         'command': {'kind': 'current-python', 'argv': ['-c', 'raise SystemExit(5)']}}
        stack['capabilities']['coverage']['suiteVerdict'] = {'exit': ['exit'], 'failure': ['findings', {'gate': 'functional'}]}
        (self.directory / 'coverage.json').write_text('{"exit": 0}', encoding='utf-8')
        rows, _, _ = self.rows(stack, ('test', 'static', 'coverage'), cap_limit=GIB + 4096)
        test, coverage = rows[0], rows[2]
        self.assertEqual(('failed', True), (coverage['state'], coverage['memoryCapHit']))
        self.assertEqual(('test', 'failed', 1, True),
                         (test['capability'], test['state'], test['resultExit'], test['memoryCapHit']))
        self.assertIn(f'memory cap hit (MemoryMax=1048580K): {FINDING}', test['reason'])

    def test_execute_rows_without_a_cap_runs_rows_unchanged(self):
        with contextlib.redirect_stdout(io.StringIO()):
            rows = self.namespace['execute_rows']([self.stack('test', 'pass')], ('test',),
                                                  self.directory.resolve(), None, [])
        self.assertEqual('passed', rows[0]['state'])

    def test_a_cap_hit_on_an_interrupted_row_still_stops_the_remaining_rows(self):
        cgroup = self.host.limit(LEAF, GIB, 0)
        cap = self.namespace['MemoryCap']('enforced', GIB, 'systemd-run --user --scope', 'fixture', cgroup)

        def run_row(stack, capability, *_):
            (cgroup / 'memory.events').write_text('oom_kill 1\n', encoding='ascii')
            return self.namespace['result'](stack, capability, 'interrupted', reason='adapter interrupted')
        stacks = [self.stack('test', 'pass'), self.stack('test', 'pass', name='later')]
        with (mock.patch.dict(self.namespace, {'run_row': run_row}), contextlib.redirect_stdout(io.StringIO()),
              contextlib.redirect_stderr(io.StringIO())):
            rows = self.namespace['execute_rows'](stacks, ('test', 'static'), self.directory.resolve(), None, [], cap)
        self.assertEqual([('hog', 'test', 'failed')], [(row['stack'], row['capability'], row['state']) for row in rows])

    # Entry points

    def fixture(self, data):
        repository = self.directory / 'repository'
        repository.mkdir()
        subprocess.run(['git', 'init', '--quiet', str(repository)], check=True)
        shutil.copyfile(self.runner, repository / 'gap_check.py')
        (repository / 'gap_check.json').write_text(json.dumps(data), encoding='utf-8')
        return repository

    def manifest(self, code='pass'):
        return {'schema': 1, 'artifactRoot': 'results', 'stacks': [self.stack('test', code)]}

    def test_the_cap_applies_to_child_running_operations_only(self):
        repository = self.fixture(self.manifest())
        record = {'state': 'unavailable', 'limit': None, 'mechanism': None, 'reason': 'fixture'}
        self.addCleanup(signal.signal, signal.SIGINT, signal.getsignal(signal.SIGINT))
        for args, applied in ((['all'], True), (['test', '--stack', 'hog'], True), (['gate', 'static'], True),
                              (['gate', 'coverage', '--stack', 'hog'], True), (['--force-run'], True),
                              ([], False), (['--help'], False), (['capabilities'], False),
                              (['all', '--bad'], False)):
            with self.subTest(args=args):
                applied_caps = []

                def memory_cap():
                    applied_caps.append(True)
                    return self.namespace['MemoryCap']('unavailable', reason='fixture')
                stdout = io.StringIO()
                with (mock.patch.dict(self.namespace, {'memory_cap': memory_cap,
                                                       '__file__': str(repository / 'gap_check.py')}),
                      contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(io.StringIO())):
                    self.namespace['main'](list(args))
                self.assertEqual(applied, bool(applied_caps))
                if applied:
                    result = next(line[8:] for line in stdout.getvalue().splitlines() if line.startswith('Result: '))
                    self.assertEqual(record, json.loads(Path(result).read_text(encoding='utf-8'))['memoryCap'])

    def test_every_result_records_the_operation_memory_cap(self):
        repository = self.fixture(self.manifest())
        process = subprocess.run([sys.executable, str(repository / 'gap_check.py'), 'all'], cwd=repository,
                                 capture_output=True, text=True, encoding='utf-8', timeout=60)
        self.assertEqual(0, process.returncode, process.stdout + process.stderr)
        result = next(line[8:] for line in process.stdout.splitlines() if line.startswith('Result: '))
        cap = json.loads(Path(result).read_text(encoding='utf-8'))['memoryCap']
        self.assertEqual({'state', 'limit', 'mechanism', 'reason'}, set(cap))
        self.assertIn(cap['state'], ('enforced', 'inherited', 'unavailable'))
        self.assertTrue(cap['limit'] is None if cap['state'] == 'unavailable' else 0 < cap['limit'] <= 8 * GIB)

    def test_dotnet_server_defaults_and_caller_values_reach_the_actual_child(self):
        names = ('MSBUILDDISABLENODEREUSE', 'DOTNET_CLI_USE_MSBUILD_SERVER', 'UseSharedCompilation')
        names = tuple(name.upper() if sys.platform == 'win32' else name for name in names)
        code = f"import json, os; open('environment.json', 'w').write(json.dumps([os.environ.get(n) for n in {names!r}]))"
        repository = self.fixture(self.manifest(code))
        clean = {key: value for key, value in os.environ.items() if key not in names}
        for supplied, expected in (({}, ['1', '0', 'false']),
                                   (dict(zip(names, ('0', '', 'true'))), ['0', '', 'true'])):
            with self.subTest(supplied=supplied):
                process = subprocess.run([sys.executable, str(repository / 'gap_check.py'), 'test', '--stack', 'hog'],
                                         cwd=repository, env={**clean, **supplied}, capture_output=True, text=True,
                                         encoding='utf-8', timeout=60)
                self.assertEqual(0, process.returncode, process.stdout + process.stderr)
                self.assertEqual(expected, json.loads((repository / 'environment.json').read_text()))

    def test_a_raised_dydo_memory_max_exits_2_before_any_child(self):
        repository = self.fixture(self.manifest("from pathlib import Path; Path('ran').write_text('x')"))
        process = subprocess.run([sys.executable, str(repository / 'gap_check.py'), 'all'], cwd=repository,
                                 env={**os.environ, 'DYDO_MEMORY_MAX': '16G'},
                                 capture_output=True, text=True, encoding='utf-8', timeout=60)
        self.assertEqual(2, process.returncode, process.stdout + process.stderr)
        self.assertIn('a cap hit is a finding; raising the cap is not allowed', process.stderr)
        self.assertFalse((repository / 'ran').exists())
        self.assertFalse((repository / 'results').exists())


class PortableMemoryCapTests(MemoryCapTests):
    runner = PORTABLE


class RunTestsMemoryCapTests(unittest.TestCase):
    """run_tests.py takes the cap from gap_check.py when it is run directly."""

    def setUp(self):
        sys.path.insert(0, str(COVERAGE))
        self.addCleanup(sys.path.remove, str(COVERAGE))
        import run_tests
        self.run_tests = run_tests
        self.MemoryCap = runpy.run_path(str(RUNNER))['MemoryCap']

    def test_a_direct_run_applies_the_cap_before_main(self):
        cap = self.MemoryCap('inherited', GIB, 'cgroup v2', 'fixture')
        facade = types.SimpleNamespace(ContractError=type('ContractError', (Exception,), {}),
                                       memory_cap=mock.Mock(return_value=cap))
        with mock.patch.dict(sys.modules, {'gap_check': facade}), mock.patch.object(self.run_tests, 'main') as main:
            self.run_tests.capped_main()
        facade.memory_cap.assert_called_once_with()
        main.assert_called_once_with(cap)

    def test_a_direct_run_refuses_a_raised_cap_with_exit_2(self):
        process = subprocess.run([sys.executable, str(COVERAGE / 'run_tests.py'), '--', '--list-tests'],
                                 cwd=ROOT, env={**os.environ, 'DYDO_MEMORY_MAX': '9G'},
                                 capture_output=True, text=True, encoding='utf-8', timeout=60)
        self.assertEqual(2, process.returncode, process.stdout + process.stderr)
        self.assertIn('a cap hit is a finding; raising the cap is not allowed', process.stderr)
        self.assertNotIn('Creating test worktree', process.stdout)

    def test_a_direct_run_that_hits_the_cap_fails_with_the_finding(self):
        directory = Path(tempfile.mkdtemp(prefix='dydo-memory-cap-'))
        self.addCleanup(shutil.rmtree, directory, True)
        events = directory / 'memory.events'
        cap = self.MemoryCap('enforced', 512 * 1024 ** 2, 'systemd-run --user --scope', 'fixture', directory)
        for kills_after, expected in (('oom_kill 4\n', 1), (None, 0)):
            with self.subTest(expected=expected):
                events.write_text('oom_kill 3\n', encoding='ascii')

                def run(**_options):
                    if kills_after:
                        events.write_text(kills_after, encoding='ascii')
                    return 0
                stderr = io.StringIO()
                with (mock.patch.object(sys, 'argv', ['run_tests.py']),
                      mock.patch.object(self.run_tests, 'contain_descendants', return_value=None),
                      mock.patch.object(self.run_tests, 'run_tests', side_effect=run),
                      contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(stderr)):
                    with self.assertRaises(SystemExit) as exited:
                        self.run_tests.main(cap)
                self.assertEqual(expected, exited.exception.code)
                self.assertEqual(expected == 1, f'memory cap hit (MemoryMax=512M): {FINDING}' in stderr.getvalue())


if __name__ == '__main__':
    unittest.main()
