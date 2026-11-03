"""Bounded passive Linux PID/fd/socket snapshots; never start or delay a checker."""
import ipaddress
import json
import os
from pathlib import Path
import threading
import time


def address(encoded, byteorder):
    raw = bytes.fromhex(encoded)
    if len(raw) not in [4, 16]:
        raise ValueError('Unknown TCP address width')
    if byteorder == 'little':
        raw = b''.join(raw[index:index + 4][::-1] for index in range(0, len(raw), 4))
    parsed = ipaddress.ip_address(raw)
    return str(parsed.ipv4_mapped) if parsed.version == 6 and parsed.ipv4_mapped else str(parsed)


def listeners(tables, targets, byteorder):
    owned = {target[8:-1] for target in targets if target.startswith('socket:[') and target.endswith(']')}
    result = []
    for name, text in tables.items():
        for line in text.splitlines()[1:]:
            fields = line.split()
            if len(fields) < 10 or fields[3] != '0A' or fields[9] not in owned:
                continue
            endpoint, port = fields[1].split(':')
            result.append(dict(table=name, address=address(endpoint, byteorder), port=int(port, 16), inode=fields[9]))
    return result


def snapshot(pid, byteorder):
    root = Path('/proc', str(pid))
    before = (root / 'stat').read_text()
    descriptors = {}
    for path in (root / 'fd').iterdir():
        try:
            descriptors[path.name] = os.readlink(path)
        except FileNotFoundError:
            continue
    tables = {name: (root / 'net' / name).read_text() for name in ['tcp', 'tcp6']}
    after = (root / 'stat').read_text()
    # The field after the final ')' is stat field3; index19 is field22/starttime.
    if before.rsplit(')', 1)[1].split()[19] != after.rsplit(')', 1)[1].split()[19]:
        raise ValueError('PID lifetime changed during observation')
    return dict(pid=pid, statBefore=before, statAfter=after, fd=descriptors, tables=tables,
                listeners=listeners(tables, descriptors.values(), byteorder))


class Passive:
    def __init__(self, process, output, byteorder):
        self.process = process
        self.output = output
        self.byteorder = byteorder
        self.stop = threading.Event()
        self.observed = []
        self.errors = []
        self.thread = threading.Thread(target=self.collect, daemon=True)

    def collect(self):
        try:
            self.collect_samples()
        except (OSError, ValueError) as error:
            self.errors.append(str(error))

    def collect_samples(self):
        deadline = time.monotonic() + 4
        with self.output.open('x', encoding='utf-8') as stream:
            for index in range(400):
                if self.process.poll() is not None or self.stop.is_set() or time.monotonic() >= deadline:
                    break
                try:
                    view = snapshot(self.process.pid, self.byteorder)
                    self.observed.extend(view['listeners'])
                    stream.write(json.dumps(dict(sample=index, monotonic=time.monotonic(), **view)) + '\n')
                except (FileNotFoundError, ProcessLookupError) as error:
                    stream.write(json.dumps(dict(sample=index, disappeared=type(error).__name__)) + '\n')
                    break
                except (OSError, ValueError) as error:
                    self.errors.append(str(error))
                    stream.write(json.dumps(dict(sample=index, error=str(error))) + '\n')
                    break
                stream.flush()
                self.stop.wait(0.01)

    def finish(self):
        self.stop.set()
        self.thread.join(timeout=5)
        if self.thread.is_alive():
            raise RuntimeError('Passive observer did not finish')
        if self.errors:
            raise RuntimeError('Passive observation failed: ' + '; '.join(self.errors))
        if any(row['address'] != '127.0.0.1' for row in self.observed):
            raise RuntimeError('Bootstrap owned a wildcard/nonlocal TCP listener')
        return self.observed
