"""Final clean-SHA whitespace/audit receipt in normal TEMP; no repository writes."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys

sys.dont_write_bytecode = True
TEMP = Path('C:/Users/User/AppData/Local/Temp')


def main():
    spec = importlib.util.spec_from_file_location('i1_checks', TEMP / 'dyd415-integration3-checks.py')
    c = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(c)
    assert all(c.sha(c.LC / p) == expected for p, expected in c.LC_HASHES.items())
    result_path = TEMP / 'dyd415-integration3-committed-check.json'
    assert not result_path.exists(), 'Never overwrite an attempt'
    head = c.git('rev-parse', 'HEAD')
    assert c.git('rev-parse', 'HEAD^') == c.START and not c.git('status', '--porcelain=v1')
    assert all(c.owned(p) for p in c.git('diff', '--name-only', c.START, head).splitlines())
    command = subprocess.list2cmdline([sys.executable, __file__, 'child'])
    receipt = dict(head=head, clean=True, driverSha256=c.sha(Path(__file__)), gates=[])
    failure = None
    with c.suite_slot.book(command, worktree=c.ROOT) as booking:
        owner = dict(pid=os.getpid(), ticket=os.environ['LC_SUITE_SLOT_TICKET'], command=command,
            namespace=str(c.suite_slot.live().directory), owned=booking.owned, inherited=booking.inherited)
        receipt['reservation'] = owner
        with c.machine_lock(c.HEAVY, on_wait=c.suite_slot.refuse_heavy_wait(c.HEAVY)):
            lock = TEMP / 'dydo-fullsuite.lock'
            with lock.open('x') as stream:
                json.dump(owner, stream)
            try:
                receipt['identities'] = c.retained()
                docs = json.loads((c.OUT / 'document-input.json').read_bytes())['files']
                assert all(c.sha(c.ROOT / p) == expected for p, expected in docs.items())
                receipt['documentsUnchanged'] = docs
                inputs = json.loads((c.OUT / 'probes-complete-input.json').read_bytes())['recipeInputs']
                paths = ['recipe/config.py', 'recipe/driver.py', 'recipe/test_recipe.py', 'dependencies-complete.json']
                assert all(c.sha(c.OUT / p) == inputs[p] for p in paths), 'Executed recipe drift'
                receipt['executedRecipeUnchanged'] = {p: inputs[p] for p in paths}
                for base in [c.START, c.MASTER]:
                    argv = ['git', 'diff', '--check', base, head]
                    run = subprocess.run(argv, cwd=c.ROOT, capture_output=True,
                        creationflags=subprocess.CREATE_NO_WINDOW)
                    receipt['gates'].append(dict(argv=argv, exit=run.returncode,
                        stdoutHex=run.stdout.hex(), stderrHex=run.stderr.hex(), hidden=True))
                    assert run.returncode == 0, 'Committed whitespace failed'
                assert not c.git('status', '--porcelain=v1')
                receipt['measurementExecuted'] = False
            except BaseException as error:
                receipt['failure'] = dict(type=type(error).__name__, message=str(error))
                failure = error
            finally:
                lock.unlink()
        receipt['heavyReleased'] = receipt['dydoReleased'] = True
    receipt['fifoReleased'] = booking.owned
    receipt['inheritedReturned'] = booking.inherited
    with result_path.open('x', encoding='utf-8', newline='\n') as stream:
        stream.write(json.dumps(receipt, indent=2) + '\n')
    print(str(result_path), flush=True)
    if failure:
        raise failure


if __name__ == '__main__':
    if sys.argv[1:] == ['child']:
        main()
    else:
        assert not sys.argv[1:]
        result = subprocess.run([sys.executable, __file__, 'child'],
            creationflags=subprocess.CREATE_NO_WINDOW)
        sys.exit(result.returncode)
