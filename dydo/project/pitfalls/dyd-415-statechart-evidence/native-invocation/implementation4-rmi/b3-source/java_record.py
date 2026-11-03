#!/usr/bin/env python3
"""Owned proof-only exec shim: bound and retain actual Java argv without changing it."""
import json
import os
from pathlib import Path
import sys

JAVA = '/toolchain/jdk-21.0.8+9/bin/java'


def claim(folder):
    for index in range(64):
        path = folder / ('java-' + str(index).zfill(2) + '.json')
        try:
            return path.open('x', encoding='utf-8')
        except FileExistsError:
            continue
    raise RuntimeError('Finite group C Java launch ceiling64 reached; no additional checker')


def main():
    folder = Path(os.environ['STATECHART_B3_JAVA_LOG'])
    if not folder.is_absolute() or not folder.resolve().is_relative_to(Path('/output').resolve()):
        raise RuntimeError('Only owned output may hold Java argv receipts')
    with claim(folder) as stream:
        json.dump(dict(pid=os.getpid(), executable=JAVA, argv=[JAVA, *sys.argv[1:]], cwd=os.getcwd(),
                       tmpdir=os.environ.get('TMPDIR'), tlc=os.environ.get('TLA2TOOLS')), stream, indent=2)
    os.execve(JAVA, [JAVA, *sys.argv[1:]], dict(os.environ))


if __name__ == '__main__':
    main()
