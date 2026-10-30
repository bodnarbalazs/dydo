"""Run only preflight inside an already admitted parent, with fresh owned output."""
import argparse
import json
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
from config import preflight


def main():
    parser = argparse.ArgumentParser()
    for name in ['source', 'work', 'out', 'normal-temp', 'metric', 'lc']:
        parser.add_argument('--' + name, required=True, type=Path)
    parser.add_argument('--tools-map', type=Path)
    args = parser.parse_args()
    sys.path.insert(0, str(args.lc))
    from tools.testing.runner import suite_slot
    command = __file__ + ' preflight'
    owner = json.loads((args.normal_temp / 'dydo-fullsuite.lock').read_bytes())
    assert owner['ticket'] == os.environ['LC_SUITE_SLOT_TICKET'], 'Admitted parent required'
    with suite_slot.book(command, worktree=args.source) as booking:
        assert booking.inherited and not booking.owned, 'Parent must hold FIFO through child exit'
        # Parent owns Heavy and dydo; do not reacquire a cross-process mutex.
        checks = preflight(args.source, args.work, args.out, args.normal_temp, args.metric,
            json.loads(args.tools_map.read_bytes()) if args.tools_map else {})
        args.out.mkdir(parents=True, exist_ok=False)
        (args.out / 'preflight.json').write_bytes((json.dumps(dict(**checks,
            inherited=True, parent=owner, hiddenLaunchByParent=True), indent=2) + '\n').encode())
    print('preflight: clean; identities exact; measurementExecuted:false')


if __name__ == '__main__':
    main()
