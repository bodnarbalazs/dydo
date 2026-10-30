"""Explicit paths and repeatable source/output checks; no writer dirty-tree attestation."""
import hashlib
import os
from pathlib import Path
import subprocess

RECIPE = Path(__file__).resolve().parent
ROOT = Path(os.environ['DYD415_SOURCE_ROOT']).resolve()
WORK = Path(os.environ['DYD415_COVERLET_WORK']).resolve()
OUT = Path(os.environ['DYD415_EVIDENCE_OUT']).resolve()
TEMP = Path(os.environ['DYD415_NORMAL_TEMP']).resolve()
PRIOR = Path(os.environ['DYD415_METRIC_ROOT']).resolve()
LC = Path(os.environ['DYD415_LC_ROOT']).resolve()
OLD = ROOT / 'dydo/project/pitfalls/dyd-415-statechart-evidence/fix2'
METRICS = PRIOR / 'metrics/bin/Release/net10.0/GateMetrics.dll'
PIN = '0ddbbf3f251a4cf5d578f4d6b84f60216efdb3b6'


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT,
        creationflags=subprocess.CREATE_NO_WINDOW).decode().strip()


def source_check():
    assert Path(git('rev-parse', '--show-toplevel')).resolve() == ROOT
    assert not git('status', '--porcelain=v1', '--untracked-files=all'), 'Source must be clean'
    assert not git('diff', '--name-only', PIN, 'HEAD', '--', 'skills'), 'Runtime pin mismatch'
    return dict(root=str(ROOT), head=git('rev-parse', 'HEAD'), runtimePin=PIN,
        clean=True, runtimeMatchesPin=True)


def preflight():
    result = source_check()
    assert ROOT.is_dir() and TEMP.is_dir() and LC.is_dir() and METRICS.is_file()
    assert WORK.is_relative_to(TEMP) and WORK != TEMP, 'Work must be below normal TEMP'
    for dest in (WORK, OUT):
        assert not dest.exists(), 'Fresh output required: ' + str(dest)
        assert not dest.is_relative_to(ROOT) and not ROOT.is_relative_to(dest), 'Keep source separate'
    assert not OUT.is_relative_to(WORK) and not WORK.is_relative_to(OUT), 'Separate work and evidence'
    assert all((RECIPE / name).is_file() for name in ('driver.py', 'finalize.py', 'report.py'))
    return dict(**result, work=str(WORK), evidence=str(OUT), recipe=str(RECIPE),
        recipeModules={p.name: hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(RECIPE.glob('*.py'))},
        metricBinary=str(METRICS), metricSha256=hashlib.sha256(METRICS.read_bytes()).hexdigest(),
        measurementExecuted=False)
