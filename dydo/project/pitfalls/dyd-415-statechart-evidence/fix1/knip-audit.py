"""Retain Knip's native file discovery alongside the issue counters."""
import json
from pathlib import Path
import subprocess

out = Path(__file__).resolve().parent
command = json.loads((out / 'knip-command.json').read_text())
with (out / 'knip-discovery.txt').open('w', encoding='utf-8') as log:
    result = subprocess.run([*command['argv'], '--debug'], cwd=command['cwd'], stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
print('Native discovery exit:', result.returncode)
