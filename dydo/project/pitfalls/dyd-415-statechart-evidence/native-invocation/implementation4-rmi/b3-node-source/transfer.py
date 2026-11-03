"""Owned network-only child. Parent bounds DNS/TLS/read time and holds all resource locks."""
import argparse
import importlib.util
import json
import os
from pathlib import Path

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location('_b3_node_transfer_driver', HERE / 'driver.py')
driver = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(driver)


def validate_context(path):
    driver.validate_host()
    context = json.loads(path.read_bytes())
    scratch = Path(context['scratch'])
    if path.resolve() != (scratch / 'context.json').resolve() or scratch.parent.resolve() != driver.TEMP.resolve() or not scratch.name.startswith('dyd415-rmi-b3-node-'):
        raise RuntimeError('Transfer context outside registered owned TEMP')
    if scratch.is_symlink() or not scratch.is_dir() or Path(context['output']).resolve() != driver.OUTPUT.resolve():
        raise RuntimeError('Transfer output/scratch identity differs')
    lock = driver.TEMP / 'dydo-fullsuite.lock'
    owner = json.loads(lock.read_bytes())
    if driver.sha(lock) != context['lockSha256'] or context['parentPid'] != os.getppid() or owner['pid'] != context['parentPid']:
        raise RuntimeError('Transfer parent/owned lock differs')
    if not context['ticket'] or context['ticket'] != os.environ.get('LC_SUITE_SLOT_TICKET') or context['ticket'] != owner['ticket']:
        raise RuntimeError('Transfer requires inherited parent FIFO ticket')
    if context['sourcePinSha256'] != driver.sha(driver.PACKET / 'source-pin.json'):
        raise RuntimeError('Transfer source pin differs')
    pin = json.loads((driver.PACKET / 'source-pin.json').read_bytes())
    release = json.loads((driver.PACKET / 'execution-release.json').read_bytes())
    if release['state'] != 'RELEASED_B3_NODE_DATA' or release['sourcePinSha256'] != context['sourcePinSha256']:
        raise RuntimeError('Transfer exact acquisition release missing')
    for row in pin['sources']:
        if driver.sha(driver.ROOT / row['path']) != row['sha256']:
            raise RuntimeError('Transfer source identity differs: ' + row['path'])
    return scratch


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--context', type=Path, required=True)
    args = parser.parse_args()
    scratch = validate_context(args.context)
    try:
        driver.transfer(scratch)
    except BaseException as error:
        driver.save('transfer-error.json', dict(type=type(error).__name__, message=str(error),
            partialDataRetained=True, retry=False))
        raise


if __name__ == '__main__':
    main()
