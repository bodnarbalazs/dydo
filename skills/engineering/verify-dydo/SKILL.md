---
name: verify-dydo
description: Verify DynaDocs CLI and guard journeys in owned disposable projects, with retained evidence. Use for runtime proof, release inquisition or a mapped regression.
---

# Verify DynaDocs

Use the [feature map](resources/features/README.md). This driver covers the CLI; the viewer,
real approval UI and pending release prerequisites need their own proof.

## Launch

From the candidate checkout, use Python 3.12+, Git and .NET 10 already installed. No authentication,
server, port or global installation is needed. Launch builds this checkout once in Release with
compiler servers disabled, creates an ordinary mock Git repository, and runs real `init claude`.
`CODEX_HOME` is redirected only for these owned CLI fixtures. Machine-mode storage is **not**
redirected by that variable or by cwd; the driver never executes a mode setter.

All commands below must run inside the host's shared suite slot. On the commissioned Windows
machine, wrap each argv using the LC checkout's existing runner (keep this parent alive):

```python
import subprocess
import sys
from pathlib import Path
sys.path.insert(0, "C:/Users/User/.t3/worktrees/LC/t3-6a75af47")
from tools.testing.runner import suite_slot
command = [sys.executable, "skills/engineering/verify-dydo/scripts/control_dydo.py", *sys.argv[1:]]
with suite_slot.book(subprocess.list2cmdline(command), worktree=Path.cwd()):
    result = subprocess.run(command, capture_output=True, text=True,
                            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    sys.stdout.write(result.stdout)
    sys.stderr.write(result.stderr)
    raise SystemExit(result.returncode)
```

Save that wrapper as `artifacts/verify-dydo/queued-control.py`; invoke it from the checkout with
`python artifacts/verify-dydo/queued-control.py launch`. Preserve inherited `LC_SUITE_SLOT_TICKET`. On another host,
use its approved queue adapter; do not invent a second lock namespace. The driver itself does
not book a slot. Direct driver invocations below describe the argv the wrapper launches.

```text
python skills/engineering/verify-dydo/scripts/control_dydo.py launch
```

The first JSON line names the allocated `run` and `evidence`. Save the absolute run path as RUN.
Exit 0 means the build, init, installed hook and version check succeeded. State persists across
calls under the system temporary directory's `dydo-verification/<run-id>/`. Each call starts
short-lived hidden children; there is no app process to keep alive. Failure retains evidence and
attempts owned scratch cleanup. A linked path causes refusal, not recursive deletion.

## Doctor

```text
python skills/engineering/verify-dydo/scripts/control_dydo.py doctor --run RUN
```

Doctor checks checkout ownership, source identity, every built artifact's hash, the mock Git
directory, generated configuration hashes, the hook installed by init and the candidate's actual
`--version`. Run before trust;
drive also runs it. Editing or committing the candidate invalidates this run: clean it and launch
again. Another checkout, an unknown run, a concurrent drive or a symlink/junction is refused.

## Drive

```text
python skills/engineering/verify-dydo/scripts/control_dydo.py drive --run RUN --journey init-docs
python skills/engineering/verify-dydo/scripts/control_dydo.py drive --run RUN --journey guard
python skills/engineering/verify-dydo/scripts/control_dydo.py drive --run RUN --journey activation
```

Only these named journeys exist. There is no arbitrary shell/CLI passthrough. Dangerous commands
and mode setters appear only in guard stdin payloads. The installed Claude marker is read from
the fixture's real init output; synthetic events test the protocol, never a user's approval UI.

## Evidence

Meet `maintain-verification-skill`'s Proof bar: observe the action and resulting state yourself,
return exact commands, raw exits and the candidate identity. Each build and fixture command writes argv,
cwd, stdin, stdout, stderr and exit to RUN/evidence. Init checks generated-file hashes and proves
a broken link fails then passes after restoration. Guard checks a sentinel survives. Activation
records its protocol-only limit; parser seam readback belongs to the separately named .NET tests.

Keep RUN/evidence and the candidate source SHA together in the handoff. The retained
`candidate.json`, build log and invocation evidence survive cleanup. Copy durable proof out
of temporary storage before its host retention expires. No secrets or external service accounts
are needed. If a drive contradicts the map, use `maintain-verification-skill`; report product
regressions without changing production code during the proof pass.

## Cleanup

```text
python skills/engineering/verify-dydo/scripts/control_dydo.py cleanup --run RUN
```

Cleanup validates the exact run and its tree, deletes only RUN/scratch, and records that scratch
is absent. It is repeatable and works after a failed launch. Confirm RUN/evidence still contains
the command logs and cleanup receipt. A busy run belongs to the live driver; do not remove its
lock or another run's resources. There are no global processes, schedules or installations to undo.

## Helpers

`scripts/control_dydo.py` is the executable helper; its entire interface is shown above. For its
boundary tests, run `python -m unittest discover -s DynaDocs.Tests/coverage/tests -p test_control_dydo.py`
through the same queue. Full .NET gates additionally obey the machine's fullsuite/heavy lock policy.
