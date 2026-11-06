#!/usr/bin/env bash
set -eu
cp DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs /tmp/dyd805-node-fixture-fixed.cjs
trap 'cp /tmp/dyd805-node-fixture-fixed.cjs DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs' EXIT
git show 530088c4007543ce545299a8005e3508a6072aaf:DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs > DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs
node --test DynaDocs.Tests/coverage/tests/guard-windows-selection.test.cjs
