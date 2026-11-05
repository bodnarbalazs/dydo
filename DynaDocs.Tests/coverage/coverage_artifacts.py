"""Attempt-qualified coverage leg artifacts: each leg stamps its upload, each union takes every leg's latest attempt.

`gh run rerun --failed` reruns only the failed legs, so one run holds a leg's artifacts from several
attempts, and download-artifact resolves a duplicate name by the largest artifact ID, which is not the
newest. Leg artifacts are therefore named per attempt. A union runs only after every leg's latest attempt
passed, and a passing leg always uploads, so each leg's highest attempt is the artifact it must consume.
"""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

IDENTITY = 'artifact-identity.json'
PLATFORMS = ('linux', 'windows')
STACKS = ('python', 'dotnet')


class SelectionError(ValueError):
    """A leg artifact is missing, duplicated or does not match its leg, attempt or candidate."""


def artifact_name(stack, platform, attempt):
    return f'{stack}-coverage-{platform}-attempt-{attempt}'


def identity(stack, platform, run_id, attempt, commit):
    return {'schema': 1, 'artifact': artifact_name(stack, platform, attempt), 'stack': stack,
            'platform': platform, 'run_id': run_id, 'run_attempt': attempt, 'commit': commit}


def stamp(results, stack, platform, run_id, attempt, commit):
    """Record which leg, run attempt and candidate produced this leg's evidence."""
    results.mkdir(parents=True, exist_ok=True)
    (results / IDENTITY).write_text(
        json.dumps(identity(stack, platform, run_id, attempt, commit), indent=2) + '\n', encoding='utf-8')


def _staged(staging, stack):
    pattern = re.compile(rf'{stack}-coverage-({"|".join(PLATFORMS)})-attempt-([0-9]+)')
    legs = {platform: {} for platform in PLATFORMS}
    for entry in sorted(staging.iterdir()) if staging.is_dir() else []:
        match = pattern.fullmatch(entry.name)
        if not match or not entry.is_dir():
            raise SelectionError(f'Unexpected artifact {entry.name!r}; a {stack} union consumes only '
                                 f'{artifact_name(stack, "<platform>", "<attempt>")} leg artifacts')
        platform, attempt = match[1], int(match[2])
        if attempt in legs[platform]:
            raise SelectionError(f'Duplicate {platform} artifacts for attempt {attempt}: '
                                 f'{legs[platform][attempt].name} and {entry.name}')
        legs[platform][attempt] = entry
    return legs


def _latest(attempts, stack, platform, run_id, run_attempt):
    if not attempts:
        raise SelectionError(f'Missing {stack} {platform} leg artifact '
                             f'{artifact_name(stack, platform, "<attempt>")}')
    attempt = max(attempts)
    if attempt > run_attempt:
        raise SelectionError(f'{attempts[attempt].name} is newer than run attempt {run_attempt}')
    path = attempts[attempt] / IDENTITY
    try:
        found = json.loads(path.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise SelectionError(f'{attempts[attempt].name} has no readable {IDENTITY}: {error}') from error
    commit = found.get('commit') if isinstance(found, dict) else None
    if not isinstance(commit, str) or not commit or found != identity(stack, platform, run_id, attempt, commit):
        raise SelectionError(f'{attempts[attempt].name} identity {found!r} does not match its leg, '
                             f'run {run_id} and attempt {attempt}')
    return attempt, commit


def select(staging, stack, run_id, run_attempt, destinations):
    """Move each leg's latest-attempt artifact to its destination and return the selection receipt."""
    legs = _staged(staging, stack)
    chosen = {platform: _latest(legs[platform], stack, platform, run_id, run_attempt) for platform in PLATFORMS}
    commits = {platform: commit for platform, (_, commit) in chosen.items()}
    if len(set(commits.values())) != 1:
        raise SelectionError(f'Leg artifacts come from different candidates: {commits}')
    occupied = [str(path) for path in destinations.values() if path.exists()]
    if occupied:
        raise SelectionError(f'Selected artifact destinations already exist: {occupied}')
    receipt = {'schema': 1, 'stack': stack, 'run_id': run_id, 'run_attempt': run_attempt,
               'commit': commits['linux'], 'legs': {}}
    for platform, (attempt, _) in chosen.items():
        destinations[platform].parent.mkdir(parents=True, exist_ok=True)
        shutil.move(legs[platform][attempt], destinations[platform])
        receipt['legs'][platform] = {
            'artifact': artifact_name(stack, platform, attempt), 'attempt': attempt,
            'superseded': [artifact_name(stack, platform, older) for older in sorted(legs[platform]) if older < attempt]}
    return receipt


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    for command in ('stamp', 'select'):
        sub = commands.add_parser(command)
        sub.add_argument('--stack', required=True, choices=STACKS)
        sub.add_argument('--run-id', required=True, type=int)
        sub.add_argument('--run-attempt', required=True, type=int)
    commands.choices['stamp'].add_argument('--platform', required=True, choices=PLATFORMS)
    commands.choices['stamp'].add_argument('--results', required=True, type=Path)
    for option in ('--staging', '--linux', '--windows', '--receipt'):
        commands.choices['select'].add_argument(option, required=True, type=Path)
    args = parser.parse_args(argv)
    if args.command == 'stamp':
        commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        stamp(args.results, args.stack, args.platform, args.run_id, args.run_attempt, commit)
        return 0
    try:
        receipt = select(args.staging, args.stack, args.run_id, args.run_attempt,
                         {'linux': args.linux, 'windows': args.windows})
    except (SelectionError, OSError) as error:
        print(error, file=sys.stderr)
        return 2
    args.receipt.parent.mkdir(parents=True, exist_ok=True)
    args.receipt.write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(receipt, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
