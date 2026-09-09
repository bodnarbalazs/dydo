---
area: general
type: changelog
date: 2026-09-09
---

# DYD-118 beta.2 dogfood refresh — 2026-09-09

Source commit `e1fdb69e7ac1d4aa42fb398e64ae2a3a5df67dfc` packaged locally as
`dydo.3.0.0-beta.2.nupkg`, SHA-256
`AEE724140CB87E9A477E6071752795844FAEB68BE590C664F2789C21B19A9E29`.
The retained beta.1 rollback package at
`C:/Users/User/Desktop/Projects/DynaDocs/dydo/agents/workspace/dyd110-evidence/dydo.3.0.0-beta.1.nupkg`
matched SHA-256 `94D24CA113737A374F13012A9504F6918935B74EE89276AC790978A23E7BD524`.

One local-only acceptance run installed the package in an isolated tool path, proved its installed
store package equals the package bytes, initialized/checked/updated/synced a scratch project and
proved the second sync identical. It upgraded the authoritative global command
`C:/Users/User/.dotnet/tools/dydo.exe`, updated the clean dogfood worktree templates, ran two syncs
and `check`, rejected any source delta, rolled back to beta.1, then reinstalled beta.2. PATH bytes
were unchanged. The final global command reports `dydo version 3.0.0-beta.2`; its executable
SHA-256 is `829BC21E8A975B85FE2974F7100C04BCF144D52D4C491FC305E8F62E7D2ECE42`.

The detailed machine record is
`dydo/_system/.local/dyd110-beta/20260909T163249521Z/evidence.json`, SHA-256
`86D47FAD3FE95C181E4E8DD7EADEC07D4662E85B1C3766C899E3E0647DD5D5D1`; its emitted-artifact
manifest SHA-256 is `AACB02758E6D91A8BCE697B427258669DB637B4F093164EFA9504617C692AE78`.
Complete merged stdout/stderr is
`dydo/_system/.local/dyd118-beta2-acceptance/acceptance.raw.log`, SHA-256
`FF2E4903A8D746B0FC00B995418E46EAFE52470D9B551BCCE96035F9E74A03F9`; the separately retained
script exit is `0` at `dydo/_system/.local/dyd118-beta2-acceptance/acceptance.exit.txt`, SHA-256
`9A271F2A916B0B6EE6CECB2426F0B3206EF074578BE55D9BC94F6F3FE3AB86AA`.

G/M and GitHub publication remain incomplete.
