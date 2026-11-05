"""Attempt-qualified coverage leg artifacts: each leg stamps its upload, each union takes every leg's latest execution.

`gh run rerun --failed` reruns only the failed legs, so one run holds a leg's artifacts from several
attempts, and download-artifact resolves a duplicate name by the largest artifact ID, which is not the
newest. Leg artifacts are therefore named per attempt. The run's own job record says in which attempt each
leg last executed: a rerun copies a leg it does not rerun into the new attempt with that attempt's number
but the original start time. The union requires exactly that attempt's artifact, and its identity and
report must name the candidate the union expects.
"""
import argparse
from datetime import datetime
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
    """A leg's execution or artifact is missing, duplicated or does not match its leg, attempt or candidate."""


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


def _time(value):
    return datetime.fromisoformat(value)


def _latest_legs(jobs, stack):
    pattern = re.compile(rf'{stack}-coverage \((.+)\)')
    legs = {}
    for job in jobs:
        match = pattern.fullmatch(job['name'])
        if not match:
            continue
        platforms = [value for value in match[1].split(', ') if value in PLATFORMS]
        if len(platforms) != 1 or platforms[0] in legs:
            raise SelectionError(f'Job {job["name"]!r} does not name one new {stack} leg platform')
        legs[platforms[0]] = job
    return legs


def leg_attempts(jobs, attempts, stack, run_attempt):
    """Each platform leg's executing attempt, from the run's latest job records and every attempt's start."""
    starts = {attempt['run_attempt']: _time(attempt['run_started_at']) for attempt in attempts}
    if sorted(starts) != list(range(1, run_attempt + 1)) or len(attempts) != run_attempt:
        raise SelectionError(f'Attempt record {sorted(starts)} does not list attempts 1..{run_attempt} once each')
    legs = _latest_legs(jobs, stack)
    executions = {}
    for platform in PLATFORMS:
        job = legs.get(platform)
        if job is None:
            raise SelectionError(f'The job record has no latest {stack} {platform} leg')
        if job['conclusion'] != 'success':
            raise SelectionError(f'The latest {stack} {platform} leg {job["name"]!r} concluded {job["conclusion"]}')
        executed = [attempt for attempt, start in starts.items() if start <= _time(job['started_at'])]
        if not executed:
            raise SelectionError(f'The latest {stack} {platform} leg started at {job["started_at"]}, before attempt 1')
        executions[platform] = max(executed)
    return executions


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


def _json(path, artifact):
    try:
        return json.loads(path.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise SelectionError(f'{artifact} has no readable {path.name}: {error}') from error


def _verified(attempts, stack, platform, run_id, attempt, commit):
    name = artifact_name(stack, platform, attempt)
    if attempt not in attempts:
        raise SelectionError(f'Missing {name}, the artifact of the {platform} leg\'s latest execution '
                             f'(staged {platform} attempts: {sorted(attempts)})')
    newer = [artifact_name(stack, platform, later) for later in sorted(attempts) if later > attempt]
    if newer:
        raise SelectionError(f'{newer} are newer than the {platform} leg\'s latest execution, attempt {attempt}')
    found = _json(attempts[attempt] / IDENTITY, name)
    if found != identity(stack, platform, run_id, attempt, commit):
        raise SelectionError(f'{name} identity {found!r} does not match its leg, run {run_id}, '
                             f'attempt {attempt} and candidate {commit}')
    report = _json(attempts[attempt] / 'adapters' / f'{stack}-coverage.json', name)
    try:
        measured = report['candidate']['commit']
    except (KeyError, TypeError):
        measured = None
    if measured != commit:
        raise SelectionError(f'{name} report measured {measured!r}, not candidate {commit}')
    return attempts[attempt]


def select(staging, stack, run_id, run_attempt, commit, executions, destinations):
    """Move each leg's latest-execution artifact to its destination and return the selection receipt."""
    legs = _staged(staging, stack)
    chosen = {platform: _verified(legs[platform], stack, platform, run_id, executions[platform], commit)
              for platform in PLATFORMS}
    occupied = [str(path) for path in destinations.values() if path.exists()]
    if occupied:
        raise SelectionError(f'Selected artifact destinations already exist: {occupied}')
    receipt = {'schema': 2, 'stack': stack, 'run_id': run_id, 'run_attempt': run_attempt, 'commit': commit,
               'legs': {}}
    for platform, artifact in chosen.items():
        destinations[platform].parent.mkdir(parents=True, exist_ok=True)
        shutil.move(artifact, destinations[platform])
        attempt = executions[platform]
        receipt['legs'][platform] = {
            'artifact': artifact_name(stack, platform, attempt), 'attempt': attempt,
            'superseded': [artifact_name(stack, platform, older) for older in sorted(legs[platform]) if older < attempt]}
    return receipt


def _lines(path):
    return [json.loads(line) for line in path.read_text(encoding='utf-8').splitlines() if line.strip()]


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
    commands.choices['select'].add_argument('--expected-commit', required=True)
    for option in ('--jobs', '--attempts', '--staging', '--linux', '--windows', '--receipt'):
        commands.choices['select'].add_argument(option, required=True, type=Path)
    args = parser.parse_args(argv)
    if args.command == 'stamp':
        commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
        stamp(args.results, args.stack, args.platform, args.run_id, args.run_attempt, commit)
        return 0
    try:
        executions = leg_attempts(_lines(args.jobs), _lines(args.attempts), args.stack, args.run_attempt)
        receipt = select(args.staging, args.stack, args.run_id, args.run_attempt, args.expected_commit,
                         executions, {'linux': args.linux, 'windows': args.windows})
    except (SelectionError, OSError, ValueError, KeyError, TypeError) as error:
        print(f'{type(error).__name__}: {error}', file=sys.stderr)
        return 2
    args.receipt.parent.mkdir(parents=True, exist_ok=True)
    args.receipt.write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(receipt, indent=2))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
