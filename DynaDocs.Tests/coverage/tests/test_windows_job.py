"""Native containment contracts, through the same owner used by JavaScript campaigns."""
import hashlib
from contextlib import ExitStack, redirect_stdout
import io
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import windows_job

MATRIX_SUBJECT = r'''
const cp=require('node:child_process'), fs=require('node:fs'), path=require('node:path');
const [api,mode,output,role='root']=process.argv.slice(2);
const mark=row=>fs.appendFileSync(path.join(output,'events.jsonl'),JSON.stringify(row)+'\n');
mark({role,pid:process.pid});
if(role==='grandchild') {
  setInterval(()=>{},1000);
} else if(role==='child') {
  if(mode==='success'||mode==='nonzero') {
    if(process.argv.at(-1)!=='empty "quotes" \\ 日本語'||process.env.NATIVE_SAFE!=='runtime-only')throw Error('argv/env');
    mark({runtime_equal:true}); process.stdout.write('out');process.stderr.write('err');
    process.exitCode=mode==='success'?0:23;
    if(process.connected){process.send({native:true});process.disconnect();}
  } else {
    const child=cp.spawn(process.execPath,[__filename,api,mode,output,'grandchild'],{detached:mode==='pipes',stdio:'inherit'});
    if(mode==='pipes'){child.unref();process.exit(0);} else setInterval(()=>{},1000);
  }
} else {
  const env={...process.env,NATIVE_SAFE:'runtime-only'}, before=JSON.stringify(env);
  const exe=mode==='refused'?path.join(output,'node.exe'):process.execPath;
  const args=[__filename,api,mode,output,'child','empty "quotes" \\ 日本語'];
  const opts={env,encoding:'utf8',windowsHide:true};
  const done=(code)=>{if(JSON.stringify(env)!==before)throw Error('mutated');mark({done:code});};
  const failed=e=>{if(e?.code==='ENOENT')mark({refused:true});else if(e)throw e;};
  if(api==='spawnSync') {const r=cp.spawnSync(exe,args,opts);failed(r.error);done(r.status);}
  else if(api==='execFileSync'){try{cp.execFileSync(exe,args,opts);done(0);}catch(e){if(e.status===23)done(23);else failed(e);}}
  else if(api==='execFile'){cp.execFile(exe,args,opts,(e)=>{if(e?.code==='ENOENT')failed(e);else done(e?.code||0);});}
  else {
    const child=api==='fork'?cp.fork(__filename,args.slice(1),{...opts,execPath:exe,silent:true}):cp.spawn(exe,args,opts);
    child.on('error',failed);child.stdout.resume();child.stderr.resume();
    child.on('message',row=>mark({ipc:row.native}));child.on('close',done);
  }
}
'''


class WindowsJobTests(unittest.TestCase):
    def setUp(self):
        evidence = os.environ.get("DYDO_NATIVE_TEST_EVIDENCE")
        self.root = Path(tempfile.mkdtemp(prefix="dydo-native-job-", dir=evidence))
        if not evidence:
            self.addCleanup(shutil.rmtree, self.root)
        (self.root / "test-id.json").write_text(json.dumps({"id": self.id()}))

    def run_subject(self, source, args=(), environment=None, budget=3):
        subject = self.root / "subject.cjs"
        subject.write_text(source, encoding="utf-8")
        request = windows_job.request([shutil.which("node"), str(subject), *args],
                                      self.root, self.root / "evidence", budget, 3)
        return windows_job.run(request, environment)

    def test_native_argv_environment_cwd_and_nonzero_are_preserved(self):
        secret = "safe-test-" + os.urandom(16).hex()
        environment = dict(os.environ, DYDO_PRIVATE_TEST=secret)
        before = dict(environment)
        source = ('if(process.argv[2]!==process.env.DYDO_PRIVATE_TEST)throw Error("env");'
                  'require("node:fs").writeFileSync("observed.json",JSON.stringify(process.argv.slice(3)));'
                  'process.stdout.write("out");process.stderr.write("err");process.exitCode=23;')
        args = [secret, "", "日本語", 'spaces "quotes" \\ ; $()']
        result = self.run_subject(source, args, environment)
        self.assertTrue(result["complete"], result)
        self.assertEqual(23, result["subject_status"])
        self.assertEqual(args[1:], json.loads((self.root / "observed.json").read_text(encoding="utf-8")))
        self.assertEqual(before, environment)
        self.assertEqual("out", Path(result["stdout_path"]).read_text())
        self.assertEqual("err", Path(result["stderr_path"]).read_text())
        self.assertNotIn(secret, (self.root / "evidence" / "result.json").read_text())

    def test_large_root_pipes_drain_concurrently(self):
        result = self.run_subject('process.stdout.write("o".repeat(1300000));process.stderr.write("e".repeat(1300000));')
        self.assertTrue(result["complete"], result)
        self.assertEqual(1300000, Path(result["stdout_path"]).stat().st_size)
        self.assertEqual(1300000, Path(result["stderr_path"]).stat().st_size)

    def test_runtime_environment_is_forwarded_without_persistent_copy(self):
        secret = "safe-test-" + os.urandom(32).hex()
        environment = dict(os.environ, DYDO_PRIVATE_TEST=secret)
        before = dict(environment)
        digest = hashlib.sha256(secret.encode()).hexdigest()
        source = ('const hash=require("node:crypto").createHash("sha256");'
                  'hash.update(process.env.DYDO_PRIVATE_TEST);'
                  'if(hash.digest("hex")!==process.argv[2])throw Error("environment changed");'
                  'process.stdout.write("forwarded");')
        result = self.run_subject(source, [digest], environment)
        self.assertTrue(result["complete"], result)
        self.assertEqual(before, environment)
        self.assertEqual(b"forwarded", Path(result["stdout_path"]).read_bytes())
        for path in self.root.rglob("*"):
            if path.is_file():
                self.assertNotIn(secret.encode(), path.read_bytes(), str(path))

    def test_root_exit_does_not_abandon_detached_inherited_pipe_descendant(self):
        result = self.run_subject('const c=require("node:child_process").spawn(process.execPath,["-e","setInterval(()=>{},1000)"],{detached:true,stdio:"inherit"});c.unref();', budget=.5)
        self.assertFalse(result["complete"])
        self.assertEqual("timeout", result["failure_category"])
        self.assertTrue(result["cleanup_confirmed"], result)
        self.assertGreaterEqual(result["job_total"], 2)

    def test_create_process_refusal_proves_no_child(self):
        request = windows_job.request([str(self.root / "absent.exe")], self.root, self.root / "evidence", 1, 1)
        result = windows_job.run(request)
        self.assertEqual("refused", result["launch_state"])
        self.assertIsNone(result["root_identity"])
        self.assertFalse(result["complete"])
        self.assertEqual(2, result["error_code"])

    def test_stale_helper_fails_before_launch(self):
        request = windows_job.request([shutil.which("node"), "-e", "process.exit(0)"], self.root, self.root / "evidence")
        request["helper_sha256"] = "0" * 64
        request["config_sha256"] = windows_job.config_hash(request)
        result = windows_job.run(request)
        self.assertEqual("not_started", result["launch_state"])
        self.assertFalse(result["complete"])
        self.assertEqual("preflight", result["failure_category"])

    def test_request_carries_no_environment(self):
        value = windows_job.request([shutil.which("node")], self.root, self.root / "evidence")
        self.assertEqual(hashlib.sha256(Path(windows_job.__file__).read_bytes()).hexdigest(), value["helper_sha256"])
        self.assertNotIn("environment", value)

    def test_boolean_protocol_version_is_not_version_one(self):
        value = windows_job.request([shutil.which("node"), "-e", "process.exitCode=0"], self.root, self.root / "evidence", 1, 1)
        value["version"] = True
        value["config_sha256"] = windows_job.config_hash(value)
        result = windows_job.run(value)
        self.assertFalse(result["complete"])
        self.assertEqual("not_started", result["launch_state"])
        self.assertFalse((self.root / "evidence").exists())

    def test_invalid_control_requests_never_launch_or_touch_outputs(self):
        changes = [("version", 2), ("argv", []), ("argv", None), ("argv", [True]),
                   ("argv", ["node\0.exe"]), ("argv", ["relative.exe"]),
                   ("cwd", str(self.root / "absent")), ("output", "relative"),
                   ("execution_seconds", 0), ("execution_seconds", 1801),
                   ("execution_seconds", True), ("teardown_seconds", 11),
                   ("owner_id", None), ("owner_id", "short"), ("owner_id", "z" * 32),
                   ("unexpected", "reject"), ("config_sha256", "0" * 64)]
        for key, change in changes:
            with self.subTest(field=key, value=change):
                value = windows_job.request([shutil.which("node"), "-e", "process.exitCode=0"], self.root, self.root / "evidence", 1, 1)
                value[key] = change
                if key != "config_sha256":
                    value["config_sha256"] = windows_job.config_hash(value)
                result = windows_job.run(value)
                self.assertFalse(result["complete"])
                self.assertEqual("not_started", result["launch_state"])
                self.assertFalse((self.root / "evidence").exists())

    def test_existing_output_is_never_overwritten(self):
        output = self.root / "evidence"
        output.mkdir()
        (output / "result.json").write_text("preserve")
        value = windows_job.request([shutil.which("node")], self.root, output)
        self.assertFalse(windows_job.run(value)["complete"])
        self.assertEqual("preserve", (output / "result.json").read_text())

    def test_unsupported_host_refuses_before_native_binding(self):
        value = windows_job.request([shutil.which("node")], self.root, self.root / "evidence")
        with patch.object(windows_job.os, "name", "posix"), patch.object(windows_job, "bindings") as native:
            self.assertFalse(windows_job.run(value)["complete"])
        native.assert_not_called()

    def test_attribute_capability_refusal_creates_no_subject(self):
        value = windows_job.request([shutil.which("node")], self.root, self.root / "evidence")
        with patch.object(windows_job, "attributes", side_effect=OSError("native capability refused")):
            result = windows_job.run(value)
        self.assertEqual("not_started", result["launch_state"])
        self.assertFalse(result["complete"])

    def test_unknown_job_observation_is_incomplete_after_confirmed_cleanup(self):
        original = windows_job.accounting
        calls = 0

        def fail_once(kernel, job):
            nonlocal calls
            calls += 1
            if calls == 1:
                raise OSError("query refused")
            return original(kernel, job)

        with patch.object(windows_job, "accounting", side_effect=fail_once):
            result = self.run_subject('setInterval(()=>{},1000);')
        self.assertFalse(result["complete"])
        self.assertTrue(result["cleanup_confirmed"], result)
        self.assertEqual("observation", result["failure_category"])

    def test_withheld_stream_confirmation_retains_incomplete_evidence(self):
        original = windows_job.poll_pipe

        def withhold(kernel, state):
            original(kernel, state)
            state["eof"] = False

        with patch.object(windows_job, "poll_pipe", side_effect=withhold):
            result = self.run_subject('process.stdout.write("complete bytes");', budget=.2)
        self.assertFalse(result["complete"])
        self.assertFalse(result["cleanup_confirmed"])
        self.assertTrue(Path(result["retained_paths"][0]).is_dir())

    def test_pending_native_reads_cancel_before_buffers_are_released(self):
        import time
        kernel = windows_job.bindings()
        with ExitStack() as stack:
            _, _, states = windows_job.pipes(stack, self.root, time.monotonic() + 2)
            for state in states:
                self.assertTrue(state["pending"])
                windows_job.close_pipe(kernel, state)
                self.assertFalse(state["pending"])
                self.assertTrue(state["closed"])
        self.assertEqual([], windows_job._unconfirmed_io)

    def test_unconfirmed_native_read_retains_memory_and_refuses_reuse(self):
        import time
        kernel = windows_job.bindings()
        states = []
        try:
            with ExitStack() as stack:
                _, _, states = windows_job.pipes(stack, self.root, time.monotonic() + 2)
                with patch.object(windows_job, "signaled", return_value=False):
                    for state in states:
                        windows_job.close_pipe(kernel, state)
                self.assertEqual(2, len(windows_job._unconfirmed_io))
                with self.assertRaisesRegex(ValueError, "refuse owner reuse"):
                    windows_job.pipes(stack, self.root, time.monotonic() + 2)
        finally:
            # The denial was at the confirmation seam; independently observe the real events before release.
            for state in states:
                self.assertTrue(windows_job.signaled(kernel, state["event"], time.monotonic() + 2))
                state["retained"] = False
                windows_job.close_pipe(kernel, state)
            windows_job._unconfirmed_io.clear()

    def test_native_read_error_never_claims_complete_streams(self):
        import ctypes
        kernel = windows_job.bindings()
        original = kernel.GetOverlappedResult
        calls = 0

        def fail_once(*args):
            nonlocal calls
            calls += 1
            if calls == 1:
                ctypes.set_last_error(5)
                return False
            return original(*args)

        kernel.GetOverlappedResult = fail_once
        with patch.object(windows_job, "bindings", return_value=kernel):
            result = self.run_subject('setInterval(()=>{},1000);', budget=.2)
        self.assertFalse(result["complete"])
        self.assertFalse(result["cleanup_confirmed"])
        self.assertTrue(result["job_empty"])
        self.assertEqual([], windows_job._unconfirmed_io)

    def test_cancellation_exceptions_retain_pending_memory_and_refuse_reuse(self):
        import time
        kernel = windows_job.bindings()
        for seam in ('cancel', 'wait'):
            with self.subTest(seam=seam):
                states = []
                try:
                    with ExitStack() as stack:
                        _, _, states = windows_job.pipes(stack, self.root, time.monotonic() + 2)
                        target, name = (kernel, 'CancelIoEx') if seam == 'cancel' else (windows_job, 'signaled')
                        with patch.object(target, name, side_effect=OSError('native cancellation observation failed')):
                            with self.assertRaises(OSError):
                                windows_job.close_pipe(kernel, states[0])
                        self.assertTrue(states[0].get('retained'))
                        self.assertIn(states[0], windows_job._unconfirmed_io)
                        self.assertTrue(states[0]['pending'])
                        with self.assertRaisesRegex(ValueError, 'refuse owner reuse'):
                            windows_job.pipes(stack, self.root, time.monotonic() + 2)
                finally:
                    for state in states:
                        if state.get('closed'):
                            continue
                        kernel.CancelIoEx(state['server'], windows_job.c.byref(state['overlapped']))
                        self.assertTrue(windows_job.signaled(kernel, state['event'], time.monotonic() + 2))
                        state['retained'] = False
                        windows_job.close_pipe(kernel, state)
                    windows_job._unconfirmed_io.clear()

    def test_teardown_exception_cannot_preserve_a_successful_completion(self):
        original = windows_job.close_pipe

        def close_then_fail(kernel, state):
            original(kernel, state)
            raise OSError('native resource release failed')

        with patch.object(windows_job, 'close_pipe', side_effect=close_then_fail):
            result = self.run_subject('process.stdout.write("completed subject");')
        self.assertEqual(0, result['subject_status'])
        self.assertFalse(result['complete'], result)
        self.assertFalse(result['cleanup_confirmed'], result)
        self.assertEqual('native_failure', result['failure_category'])
        self.assertEqual(result, json.loads((self.root / 'evidence/result.json').read_text()))

    def test_native_handle_close_refusal_cannot_claim_confirmed_cleanup(self):
        kernel = windows_job.bindings()
        original = kernel.CloseHandle
        refused = []

        def close_then_report_refusal(handle):
            closed = original(handle)
            if not refused:
                refused.append(handle)
                windows_job.c.set_last_error(5)
                return False
            return closed

        kernel.CloseHandle = close_then_report_refusal
        with patch.object(windows_job, 'bindings', return_value=kernel):
            result = self.run_subject('process.stdout.write("native completion");')
        self.assertEqual(1, len(refused))
        self.assertFalse(result['complete'], result)
        self.assertFalse(result['cleanup_confirmed'], result)
        self.assertEqual('native_failure', result['failure_category'])

    def test_main_uses_actual_owner_and_returns_only_allowlisted_metadata(self):
        value = windows_job.request([shutil.which("node"), "-e", "process.exitCode=23"], self.root, self.root / "evidence", 2, 2)
        stream = io.StringIO()
        with patch.object(sys, "argv", ["windows_job.py"]), patch.object(sys, "stdin", io.StringIO(json.dumps(value))), redirect_stdout(stream):
            self.assertEqual(0, windows_job.main())
        result = json.loads(stream.getvalue())
        self.assertEqual(23, result["subject_status"])
        self.assertTrue(result["complete"])
        self.assertNotIn("environment", result)

    def test_main_capability_preflight_creates_no_subject(self):
        stream = io.StringIO()
        with patch.object(sys, "argv", ["windows_job.py", "--preflight"]), redirect_stdout(stream):
            self.assertEqual(0, windows_job.main())
        result = json.loads(stream.getvalue())
        self.assertTrue(result["capability"])
        self.assertEqual([3, 12, 14], result["version"])
        self.assertEqual(hashlib.sha256(Path(windows_job.__file__).read_bytes()).hexdigest(), result["helper_sha256"])

    def test_main_malformed_request_is_incomplete_without_native_launch(self):
        stream = io.StringIO()
        with patch.object(sys, "argv", ["windows_job.py"]), patch.object(sys, "stdin", io.StringIO("{")), redirect_stdout(stream):
            self.assertEqual(2, windows_job.main())
        self.assertEqual("not_started", json.loads(stream.getvalue())["launch_state"])

    def test_nested_owner_timeout_cleans_its_subtree_inside_outer_job(self):
        observer = self.root / "nested.py"
        observer.write_text('import sys,json\nfrom pathlib import Path\nsys.path.insert(0,sys.argv[1])\nimport windows_job\nr=Path(sys.argv[2]);v=windows_job.request([sys.argv[3],"-e","setInterval(()=>{},1000)"],r,r/"inner",.2,2)\n(r/"inner-observed.json").write_text(json.dumps(windows_job.run(v)))\n', encoding="utf-8")
        value = windows_job.request([sys._base_executable, str(observer), str(Path(windows_job.__file__).parent),
                                     str(self.root), shutil.which("node")], self.root, self.root / "outer", 3, 3)
        result = windows_job.run(value)
        self.assertTrue(result["complete"], result)
        inner = json.loads((self.root / "inner-observed.json").read_text())
        self.assertFalse(inner["complete"])
        self.assertTrue(inner["cleanup_confirmed"])
        self.assertEqual("timeout", inner["failure_category"])


def matrix_case(api, mode):
    def exercise(self):
        result = self.run_subject(MATRIX_SUBJECT, [api, mode, str(self.root)], budget=1.5)
        events = [json.loads(line) for line in (self.root / "events.jsonl").read_text().splitlines()]
        self.assertTrue(result["cleanup_confirmed"], result)
        self.assertTrue(result["job_empty"])
        self.assertTrue(result["streams_complete"])
        self.assertTrue(all(row["signaled"] for row in result["held_handles"]))
        if mode in ("hang", "pipes"):
            self.assertFalse(result["complete"])
            self.assertEqual("timeout", result["failure_category"])
            self.assertGreaterEqual(result["job_total"], 3)
            self.assertEqual({"root", "child", "grandchild"}, {row["role"] for row in events if "role" in row})
            return
        self.assertTrue(result["complete"], result)
        self.assertEqual(0, result["subject_status"])
        if mode == "refused":
            self.assertTrue(any(row.get("refused") for row in events))
            self.assertEqual(["root"], [row["role"] for row in events if "role" in row])
            return
        self.assertTrue(any(row.get("runtime_equal") for row in events))
        status = {"success": 0, "nonzero": 23}[mode]
        self.assertTrue(any(row.get("done") == status for row in events))
        if api == "fork":
            self.assertTrue(any(row.get("ipc") for row in events))
    return exercise


for _api in ("spawn", "spawnSync", "execFile", "execFileSync", "fork"):
    for _mode in ("success", "nonzero", "hang", "pipes", "refused"):
        setattr(WindowsJobTests, "test_native_" + _api + "_" + _mode, matrix_case(_api, _mode))


if __name__ == "__main__":
    unittest.main()
