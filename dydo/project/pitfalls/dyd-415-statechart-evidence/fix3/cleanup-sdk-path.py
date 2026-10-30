"""Remove only the unique tool-path entry added by this task's SDK first use."""
import hashlib
import json
import os
from pathlib import Path
import winreg

assert os.name == 'nt'
out = Path('C:/Users/User/.t3/worktrees/DynaDocs/t3-ba795f5f/dydo/project/pitfalls/dyd-415-statechart-evidence/fix3')
owned = r'C:\Users\User\AppData\Local\Temp\dyd415-fix3\dotnet-home\.dotnet\tools'
with winreg.OpenKey(winreg.HKEY_CURRENT_USER, 'Environment', 0, winreg.KEY_READ | winreg.KEY_WRITE) as key:
    before, kind = winreg.QueryValueEx(key, 'Path')
    parts = before.split(';')
    removed = [part for part in parts if part.casefold() == owned.casefold()]
    assert len(removed) == 1, 'Refuse ambiguous ownership or a repeated cleanup'
    kept = [part for part in parts if part.casefold() != owned.casefold()]
    after = ';'.join(kept)
    assert winreg.QueryValueEx(key, 'Path') == (before, kind), 'User PATH changed concurrently'
    winreg.SetValueEx(key, 'Path', 0, kind, after)
    observed, observed_kind = winreg.QueryValueEx(key, 'Path')
    assert observed == after and observed_kind == kind
    assert observed.split(';') == kept
receipt = dict(removedOwnedEntry=removed, removedCount=1, otherEntriesPreserved=True,
    registryValueTypePreserved=True, taskEntryAbsent=True, certificateStoreModified=False,
    beforeSha256=hashlib.sha256(before.encode()).hexdigest(),
    afterSha256=hashlib.sha256(after.encode()).hexdigest())
(out / 'sdk-path-cleanup.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
print(json.dumps(receipt, indent=2))
