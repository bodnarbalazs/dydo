"""One ordinary coverage.py collector and callable witness per Python process."""
import argparse
import atexit
import errno
import hashlib
import json
import os
import subprocess
import sys
import uuid
from pathlib import Path

_ACTIVE = None
_UNRECORDED_FAILURE_EXIT = 70  # EX_SOFTWARE
# Once the command has returned, how long `collect` waits for measured processes still connected to close.
# A descendant nobody waited for may still be in its exit `finish` (coverage save and receipt write, well
# under a second on an idle host); five seconds leaves room for a loaded runner. Only a connection still
# open makes `collect` wait, so a campaign whose processes have all exited pays nothing.
_SETTLE_SECONDS = 5
# How a process opens its reporting connection. `collect` listens on loopback for the whole command, so a
# connect normally completes in well under a millisecond. It can fail for a moment: no ephemeral port free
# yet (EADDRNOTAVAIL), or a full backlog refusing or resetting it (Windows) or dropping its SYN, which a
# Linux client would resend only after a second, so each attempt stops after `_CONNECT_TIMEOUT_SECONDS`.
# After each such failure the process waits the next backoff and connects anew; four attempts take 0.47 s
# at most (4 x 0.1 s + 0.07 s), and a process that connects at once pays nothing more.
_CONNECT_TIMEOUT_SECONDS = 0.1
_CONNECT_BACKOFF_SECONDS = (0.01, 0.02, 0.04)
# Loopback buffer/memory pressure or an interrupted/would-block send can clear quickly. Give the full
# report 250 ms (well below the collector's settle), sleeping 10 ms between attempts rather than spinning.
_REPORT_RETRY_SECONDS = 0.25
_REPORT_BACKOFF_SECONDS = 0.01
_REPORT_TRANSIENT = {getattr(errno, name) for name in (
    "ENOBUFS", "ENOMEM", "EAGAIN", "EWOULDBLOCK", "EINTR", "WSAENOBUFS", "WSAEWOULDBLOCK", "WSAEINTR")
    if hasattr(errno, name)}
_REPORT_GONE = {getattr(errno, name) for name in (
    "EPIPE", "ECONNRESET", "ECONNABORTED", "EBADF", "ENOTCONN", "ENOTSOCK", "ESHUTDOWN", "ETIMEDOUT",
    "WSAECONNRESET", "WSAECONNABORTED", "WSAENOTCONN", "WSAENOTSOCK", "WSAESHUTDOWN", "WSAETIMEDOUT")
    if hasattr(errno, name)}
# Every measured process's startup. Without a campaign config it imports nothing. With one, a
# `python_coverage` that cannot be imported ends the process before its program runs, where `site` would
# run it unmeasured; `os` and `sys` are loaded already, so that exit takes nothing new. An Exception that
# escapes `startup_from_environment` has already reached `collect`; other BaseExceptions abort interpreter
# startup before the program runs, as does failing to open or name the reporting connection.
_SITECUSTOMIZE = """\
import os
import sys
if os.environ.get("DYDO_PYTHON_COVERAGE_CONFIG"):
    try:
        from python_coverage import startup_from_environment
    except BaseException as error:
        try:
            print(f"Python measurement failed in process {os.getpid()} at startup ({sys.orig_argv}): "
                  f"{type(error).__name__}: {error}; it could not be recorded: python_coverage did not import",
                  file=sys.stderr, flush=True)
        finally:
            os._exit(%d)
    startup_from_environment()
""" % _UNRECORDED_FAILURE_EXIT


def _identity(row):
    return row["path"], row["id"]


def _shape(row):
    return {key: value for key, value in row.items()
            if key not in ("execution_count", "body_lines", "branches", "physical_branches")}


def _hits(value):
    if type(value) is not int or value < 0:
        raise ValueError("Invalid Python execution counter")
    return value


def _original_counters(receipt, kind):
    rows = receipt.get(kind, [])
    original = {_identity(row): row for row in rows}
    if len(original) != len(rows):
        raise ValueError("Duplicate Python counter identity")
    return original


def _copy_counters(original):
    return {identity: {**row, "body_lines": dict(row["body_lines"]),
                       "branches": list(row["branches"]),
                       "physical_branches": list(row["physical_branches"])}
            for identity, row in original.items()}


def _merge_counter_row(target, expected, row):
    if _shape(row) != _shape(expected) or set(row["body_lines"]) != set(expected["body_lines"]):
        raise ValueError("Python counter body inventory mismatch")
    target["execution_count"] += _hits(row["execution_count"])
    for line, hits in row["body_lines"].items():
        target["body_lines"][line] += _hits(hits)
    for branch_kind in ("branches", "physical_branches"):
        for edge in row[branch_kind]:
            if edge not in target[branch_kind]:
                target[branch_kind].append(edge)


def _merge_receipt(first, receipt, kind, original, accumulated):
    if receipt.get("schema") != 1 or receipt.get("sources") != first["sources"]:
        raise ValueError("Python counter source inventory mismatch")
    rows = receipt.get(kind, [])
    actual = {_identity(row): row for row in rows}
    if len(actual) != len(rows) or set(actual) != set(original):
        raise ValueError("Python counter callable inventory mismatch")
    for identity, row in actual.items():
        _merge_counter_row(accumulated[identity], original[identity], row)


def _combine_counter_kind(first, receipts, kind):
    original = _original_counters(first, kind)
    accumulated = _copy_counters(original)
    for receipt in receipts:
        _merge_receipt(first, receipt, kind, original, accumulated)
    return [accumulated[key] for key in sorted(accumulated)]


def combine_counters(receipts):
    """Union flat per-process counters after exact complete source/body matching."""
    if not receipts:
        raise ValueError("Missing Python counter files")
    first = receipts[0]
    if first.get("schema") != 1 or not isinstance(first.get("sources"), dict):
        raise ValueError("Invalid Python counter schema")
    output = {"schema": 1, "sources": first["sources"], "callables": [], "modules": []}
    for kind in ("callables", "modules"):
        output[kind] = _combine_counter_kind(first, receipts[1:], kind)
    return output


def _inventory_cache(config):
    """The campaign's prebuilt inventory, or None (a miss, counted fresh) when it cannot be read."""
    try:
        return Path(config["inventory"]).read_bytes()
    except (KeyError, TypeError, ValueError, OSError):
        return None


def _record(text):
    """One record on a reporting connection: a JSON string on a line of its own."""
    return (json.dumps(text) + "\n").encode("ascii")


def _failure(phase, error):
    return (f"Python measurement failed in process {os.getpid()} at {phase} "
            f"({sys.orig_argv}): {type(error).__name__}: {error}")


def _exit_unrecorded(phase, error, unrecorded):
    """End a process whose failed measurement no campaign can hear: `site` and `atexit` swallow the error,
    so only a nonzero exit is left. Telling stderr is best effort; the exit needs no new resource."""
    try:
        print(f"{_failure(phase, error)}; it could not be recorded: {unrecorded}", file=sys.stderr, flush=True)
        sys.stdout.flush()
    finally:
        os._exit(_UNRECORDED_FAILURE_EXIT)


def _transient(error):
    """A connect failure that can pass in moments; any other leaves the process without a connection."""
    import errno
    return isinstance(error, (ConnectionError, TimeoutError)) or error.errno == errno.EADDRNOTAVAIL


def _connect_once(port):
    import socket
    connection = socket.socket()
    try:
        connection.settimeout(_CONNECT_TIMEOUT_SECONDS)
        connection.connect(("127.0.0.1", port))
    except OSError:
        connection.close()
        raise
    connection.settimeout(None)
    return connection


def _connect(config_path):
    """A connection to `collect` on the port in the environment or, for a process that lost that variable,
    in its campaign config, retrying transient failures as `_CONNECT_BACKOFF_SECONDS` allows."""
    import time
    port = int(os.environ.get("DYDO_PYTHON_COVERAGE_FAILURES")
               or json.loads(Path(config_path).read_text(encoding="utf-8"))["port"])
    for backoff in _CONNECT_BACKOFF_SECONDS:
        try:
            return _connect_once(port)
        except OSError as error:
            if not _transient(error):
                raise
        time.sleep(backoff)
    return _connect_once(port)


def _report_peer_gone(error):
    return isinstance(error, ConnectionError) or error.errno in _REPORT_GONE


def _report_exhausted(connection):
    """Try a small failure frame; if even that cannot go, hold the peer for collect's settle verdict.

    A parent waiting for this process also waits for the collector to close: abandoning the connection
    would be indistinguishable from a killed child. No further sends are attempted on this path."""
    import time
    try:
        connection.sendall(_record(f"Python process {os.getpid()}: reporting retries exhausted"))
        return
    except OSError as error:
        if _report_peer_gone(error):
            raise
    while True:
        try:
            if not connection.recv(1):
                raise BrokenPipeError("the collector closed the reporting connection")
        except OSError as error:
            if _report_peer_gone(error):
                raise
            time.sleep(_REPORT_BACKOFF_SECONDS)


class _Report:
    """This process's reporting connection to its campaign's `collect`, opened before its program runs.

    Its first record names the process. A failed measurement follows on the same connection, so sending it
    takes no new descriptor, and closes it; a clean exit closes it with nothing more. A process that cannot
    open the connection exits at startup, so no measured program runs without one."""

    def __init__(self, config_path):
        try:
            self.connection = _connect(config_path)
            self.connection.sendall(_record(f"process {os.getpid()} {sys.orig_argv}"))
        except BaseException as error:
            _exit_unrecorded("startup", error, "no reporting connection")

    def failed(self, phase, error):
        """Keep the connection until failure is observable or its peer is gone.

        Killed children, never-ran programs whose exits/output a parent ignores, and asynchronous signals
        during reporting are the three honest limits; a transient socket error is not a killed child.
        """
        import time
        deadline = time.monotonic() + _REPORT_RETRY_SECONDS
        exhausted = False
        interrupted = None
        while True:
            try:
                if exhausted:
                    _report_exhausted(self.connection)
                else:
                    self.connection.sendall(_record(_failure(phase, error)))
                break
            except OSError as unrecorded:
                if _report_peer_gone(unrecorded):
                    _exit_unrecorded(phase, error, unrecorded)
                exhausted = unrecorded.errno not in _REPORT_TRANSIENT or time.monotonic() >= deadline
                time.sleep(_REPORT_BACKOFF_SECONDS)
            except BaseException as interruption:
                # Never silently close after an interruption, however many recur. A partial frame
                # followed by a full one is unreadable, which also fails the campaign closed.
                if interrupted is None:
                    interrupted = interruption
        self.connection.close()
        if interrupted is not None:
            raise interrupted


def startup_from_environment():
    """Start once in the current process when the invocation config is inherited."""
    config_path = os.environ.get("DYDO_PYTHON_COVERAGE_CONFIG")
    if not config_path or _ACTIVE is not None:
        return _ACTIVE
    report = _Report(config_path)
    try:
        return _start(config_path, report)
    except Exception as error:
        # site swallows Exception and runs the program; other BaseExceptions already abort startup.
        report.failed("startup", error)
        raise


def _start(config_path, report):
    global _ACTIVE
    config = json.loads(Path(config_path).read_text(encoding="utf-8"))
    # A process of the collecting build started outside its venv (`sys._base_executable`) has no coverage.py of
    # its own. It finds the collector's in `packages`, searched after its own path and only for this import,
    # restoring search-path precedence for unrelated packages. Instrumentation dependencies stay imported.
    packages = config.get("packages", [])
    sys.path += packages
    try:
        import coverage
    finally:
        del sys.path[len(sys.path) - len(packages):]
    from python_runtime import CallableWitness
    if config.get("schema") != 1 or coverage.__version__ != "7.16.0":
        raise RuntimeError("Python campaign requires schema 1 and coverage.py 7.16.0")
    sources = {path: Path(path).read_text(encoding="utf-8-sig") for path in config["sources"]}
    cache = _inventory_cache(config)
    collector = coverage.Coverage(data_file=str(Path(config["output"]) / ".coverage"),
                                  data_suffix=True, branch=True, source=[config["root"]],
                                  config_file=False)
    witness = CallableWitness(sources, cache)
    collector.start()
    witness.__enter__()
    _ACTIVE = collector, witness

    def finish():
        try:
            write_receipt()
        except BaseException as error:
            report.failed("finish", error)
            raise
        report.connection.close()

    def write_receipt():
        global _ACTIVE
        if _ACTIVE is None:
            return
        collector.stop()
        witness.__exit__(None, None, None)
        collector.save()
        receipt = {
            "schema": 1,
            "sources": {path: hashlib.sha256(text.encode("utf-8")).hexdigest()
                        for path, text in sources.items()},
            "callables": witness.rows(), "modules": witness.module_rows(),
        }
        output = Path(config["output"]) / f"counter-{os.getpid()}-{uuid.uuid4().hex}.json"
        output.write_text(json.dumps(receipt, sort_keys=True), encoding="utf-8")
        _ACTIVE = None

    atexit.register(finish)
    return _ACTIVE


class _Peer:
    """One reporting connection as `collect` reads it: the process it named, and any record not yet complete."""

    def __init__(self, connection):
        self.connection, self.name, self.pending = connection, None, b""

    def __str__(self):
        return self.name or "campaign connection that named no process"

    def read(self, failures):
        """Take what the connection sent; False once it has closed."""
        try:
            data = self.connection.recv(65536)
        except ConnectionError:  # a killed process may reset its connection rather than close it
            data = b""
        *records, self.pending = (self.pending + data).split(b"\n")
        for record in records:
            self._take(record, failures)
        if not data and self.pending:
            failures.append(f"Python {self} closed its connection inside an incomplete record")
        return bool(data)

    def _take(self, record, failures):
        try:
            text = json.loads(record)
        except ValueError:
            text = None
        if not isinstance(text, str):
            failures.append(f"Python {self} sent an unreadable record: {record!r}")
        elif self.name is None:
            self.name = text
        else:
            failures.append(text)

    def lingering(self):
        """The failure of a connection still open once the campaign has settled."""
        return (f"Python {self} was still connected {_SETTLE_SECONDS} s after the campaign's command ended"
                + (", inside an incomplete record" if self.pending else ""))


def _accept(listener, selector, peers):
    import selectors
    try:
        connection, _ = listener.accept()
    except ConnectionError:  # reset by a killed process before it could be accepted
        return
    peer = _Peer(connection)
    peers.add(peer)
    selector.register(connection, selectors.EVENT_READ, peer)


def _serve(listener, woken, failures, served):
    """Read every reporting connection until the command has ended and each has closed.

    Once woken at the command's end it waits `_SETTLE_SECONDS` at most; a connection still open then is
    a failure. No read waits on a peer: each follows the selector reporting data or a close."""
    import selectors
    import time
    peers, deadline = set(), None
    with selectors.DefaultSelector() as selector:
        selector.register(listener, selectors.EVENT_READ)
        selector.register(woken, selectors.EVENT_READ)
        while deadline is None or (peers and time.monotonic() < deadline):
            for key, _ in selector.select(None if deadline is None else deadline - time.monotonic()):
                if key.fileobj is woken:
                    selector.unregister(woken)
                    deadline = time.monotonic() + _SETTLE_SECONDS
                elif key.fileobj is listener:
                    _accept(listener, selector, peers)
                elif not key.data.read(failures):
                    selector.unregister(key.fileobj)
                    key.fileobj.close()
                    peers.discard(key.data)
    for peer in peers:
        failures.append(peer.lingering())
        peer.connection.close()
    served.set()


def _run_reported(argv, root, env, config, config_path):
    """Run the campaign command while each measured process holds a reporting connection to `collect`.

    The port reaches each process in the environment and is mirrored in the campaign config, so a process
    that lost the variable still finds it. Returns the command's exit and every failure the connections
    showed; a reader that stopped early fails the campaign instead."""
    import socket
    import threading
    failures, served = [], threading.Event()
    listener = socket.create_server(("127.0.0.1", 0), backlog=socket.SOMAXCONN)
    wake, woken = socket.socketpair()
    with listener, wake, woken:
        port = listener.getsockname()[1]
        config_path.write_text(json.dumps({**config, "port": port}, indent=2), encoding="utf-8")
        reader = threading.Thread(target=_serve, args=(listener, woken, failures, served), daemon=True)
        reader.start()
        try:
            returncode = subprocess.run(argv, cwd=root,
                                        env={**env, "DYDO_PYTHON_COVERAGE_FAILURES": str(port)}).returncode
        finally:
            wake.sendall(b"\0")
            reader.join(2 * _SETTLE_SECONDS)
    if not served.is_set():
        raise ValueError("Python measurement reports stopped being read before the campaign settled")
    return returncode, failures


def collect(root, output, source_paths, command):
    """Run one ordinary command, then combine native data and flat process counters."""
    import coverage
    import sysconfig
    from python_runtime import inventory_cache
    root, output = Path(root).resolve(), Path(output).resolve()
    if output.exists() or not source_paths or not command:
        raise ValueError("Python campaign requires a new output and nonempty sources/command")
    output.mkdir(parents=True)
    sources = []
    for relative in source_paths:
        path = (root / relative).resolve()
        if not path.is_relative_to(root) or not path.is_file():
            raise ValueError(f"Invalid Python target source: {relative}")
        sources.append(str(path))
    packages = dict.fromkeys(sysconfig.get_paths()[scheme] for scheme in ("purelib", "platlib"))
    config = {"schema": 1, "root": str(root), "output": str(output), "sources": sources, "packages": [*packages]}
    cache = inventory_cache({path: Path(path).read_text(encoding="utf-8-sig") for path in sources})
    if cache is not None:  # None: it did not read back as computed, so every process computes fresh
        (output / "inventory.pickle").write_bytes(cache)
        config["inventory"] = str(output / "inventory.pickle")
    startup = output / "startup"
    startup.mkdir()
    (startup / "sitecustomize.py").write_text(_SITECUSTOMIZE, encoding="utf-8")
    env = dict(os.environ)
    config_path = output / "config.json"
    env["DYDO_PYTHON_COVERAGE_CONFIG"] = str(config_path)
    tools = Path(__file__).resolve().parent
    env["PYTHONPATH"] = os.pathsep.join([str(startup), str(tools), env.get("PYTHONPATH", "")])
    argv = [sys.executable if item == "{python}" else item for item in command]
    returncode, failures = _run_reported(argv, root, env, config, config_path)
    if failures:
        raise ValueError("; ".join(failures))
    data = coverage.Coverage(data_file=str(output / ".coverage"), branch=True,
                             source=[str(root)], config_file=False)
    data.combine(data_paths=[str(output)], strict=True, keep=True)
    data.json_report(morfs=sources, outfile=str(output / "coverage.json"))
    receipts = [json.loads(path.read_text(encoding="utf-8"))
                for path in sorted(output.glob("counter-*.json"))]
    combined = combine_counters(receipts)
    (output / "counters.json").write_text(
        json.dumps(combined, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    return returncode


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--sources-json", required=True)
    parser.add_argument("--command-json", required=True)
    args = parser.parse_args()
    try:
        return collect(args.root, args.output, json.loads(args.sources_json),
                       json.loads(args.command_json))
    except (ValueError, OSError, KeyError, TypeError) as error:
        print(error, file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
