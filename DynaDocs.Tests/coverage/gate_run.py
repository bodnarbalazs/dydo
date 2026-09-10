"""Independent collectors and immutable command evidence for the universal gate."""
import hashlib
import re
import subprocess
import time
from pathlib import Path

from run_tests import isolated_environment


def file_hash(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


class CommandLog:
    def __init__(self, root, output):
        self.root = Path(root).resolve()
        self.output = Path(output).resolve()
        self.output.mkdir(parents=True, exist_ok=True)
        self.rows = []

    def run(self, name, command, *, cwd=None, environment=None, stdin=None, timeout=900):
        if not re.fullmatch(r'[A-Za-z0-9_-]+', name) or not command:
            raise ValueError('Invalid gate command identity')
        prefix = self.output / f'{len(self.rows):04d}-{name}'
        stdout, stderr = Path(str(prefix) + '.stdout'), Path(str(prefix) + '.stderr')
        env = isolated_environment()
        env.update(environment or {})
        row = {'name': name, 'command': list(map(str, command)), 'cwd': str(Path(cwd or self.root).resolve()),
               'stdout': str(stdout), 'stderr': str(stderr)}
        started = time.monotonic()
        self.rows.append(row)
        try:
            result = subprocess.run(row['command'], cwd=row['cwd'], env=env, input=stdin,
                                    text=True, encoding='utf-8', errors='strict', capture_output=True, timeout=timeout)
            stdout.write_text(result.stdout, encoding='utf-8')
            stderr.write_text(result.stderr, encoding='utf-8')
            row.update(exit_code=result.returncode, stdout_sha256=file_hash(stdout), stderr_sha256=file_hash(stderr))
        except Exception as error:
            row.update(exit_code=None, error=f'{type(error).__name__}: {error}')
            raise
        finally:
            row['duration_seconds'] = round(time.monotonic() - started, 6)
        return row


def checked_result(value):
    if not isinstance(value, dict) or not {'status', 'facts', 'findings', 'errors'} <= set(value):
        raise ValueError('Missing collector result fields')
    if not isinstance(value['facts'], dict) or not isinstance(value['findings'], list) or not isinstance(value['errors'], list):
        raise ValueError('Invalid collector result schema')
    status = value['status']
    if status not in ('pass', 'fail', 'error'):
        raise ValueError('Unknown collector status')
    if status == 'pass' and (value['findings'] or value['errors']):
        raise ValueError('Collector pass hides findings or missing measurement')
    if status == 'fail' and (not value['findings'] or value['errors']):
        raise ValueError('Invalid measured policy failure')
    if status == 'error' and not value['errors']:
        raise ValueError('Missing measurement error identity')
    return value


def result(facts=None, findings=None, errors=None):
    findings, errors = findings or [], errors or []
    status = 'pass'
    if findings:
        status = 'fail'
    if errors:
        status = 'error'
    return {'status': status,
            'facts': facts or {}, 'findings': findings, 'errors': errors}


def collect_all(collectors, required):
    if not required or len(set(required)) != len(required):
        raise ValueError('Missing or duplicate required collector inventory')
    rows = {}
    for name in required:
        try:
            if name not in collectors:
                raise ValueError(f'Missing required collector: {name}')
            rows[name] = checked_result(collectors[name]())
        except Exception as error:
            rows[name] = result(errors=[{'type': type(error).__name__, 'message': str(error)}])
    if set(collectors) - set(required):
        rows['unregistered'] = result(errors=[{'message': 'Unregistered collector names', 'names': sorted(set(collectors) - set(required))}])
    code = max({'pass': 0, 'fail': 1, 'error': 2}[row['status']] for row in rows.values())
    return {'version': 1, 'collectors': rows, 'exit_code': code, 'measurement_complete': code != 2}
