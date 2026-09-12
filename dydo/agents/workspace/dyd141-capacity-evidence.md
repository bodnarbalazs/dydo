# DYD-141 capacity evidence

## Preserved local configuration

- Path: `C:/Users/User/.codex/config.toml`.
- Key/value: `agents.max_concurrent_threads_per_session = 16`.
- Backup: `C:/Users/User/.codex/config.toml.dyd141-20260908-233410.bak`.
- SHA-256 before: `A4C7BD53E1FAAD2485F888383DBCAD7315C374A31796C43FE94FE775911B783F`.
- SHA-256 after: `55AA09BA27184BDE752339A0E89B3BAA99AE58DEF1811572FB9886CC1C0574B9`.

The value is schema-valid. Its consumption or effect in this existing task is unproved. The observed
reopened-stage admission is the only later acceptance recorded here; it does not prove reload,
reclamation, backend, desktop or CLI version, model, engine, or lifecycle semantics. Durable project
configuration emission remains DYD-86 work and broader lifecycle claims remain DYD-88 work.

## Bounded refusal and delivery chain

Before reopen, a completed researcher disappeared before or with one accepted replacement spawn. Later,
at four live inventory entries, the necessary fresh captain spawn and that captain's necessary fresh
SPEC-review spawn were refused. The acceptance may therefore be ordinary reclamation. The refusal record
preserves the exact candidate, hop SHA, and brief; it does not authorize an Admiral-to-crew dispatch or
a retry.

After reopen, the Admiral admitted the Issue Captain. The captain accepted a fresh SPEC reviewer, which
accepted. It then accepted a fresh code-writer strictly serially; that worker accepted and completed
candidate `9d6179ca881763a6cbc0c8c136d05c31931de6e2`. The harden-hop hardener (`059c575f`) was
accepted after the writer. That captain's one bounded whole-Issue CODE-reviewer spawn was then refused
at four retained entries; it made no retry and released the Issue. This is the actual captain chain
recorded by the desktop task; it does not complete the later review gate. On a second host, a Claude
Code session, a fresh captain resumed from the record: CODE reviewer 1 was accepted and returned FAIL
with two findings, a fix specifier and this fix hardener were accepted serially, and this hop's two
`DYD-141 fix:` commits follow. CODE reviewer 2 is recorded on the Linear record after this hop, not here.

### Necessary spawns and the inventory observed before each

Every cell is what the Linear DYD-141 record (comment IDs, first eight characters) or the untracked
observation note `dydo/agents/workspace/20260908-review-capacity-observation.md` states; nothing is
inferred. "Not recorded" means the record holds no count for that spawn. Host A is the Codex desktop
task of 2026-09-08 (four slots advertised including root; the exposed API has spawn, follow-up, send,
list, wait, interrupt and no close/delete primitive). Host B is the Claude Code session of 2026-09-09.
Model values are requested/configured only; effective identities are unproved on both hosts.

| # | Host | Parent -> child | Inventory observed before it | Outcome | Record |
|---|---|---|---|---|---|
| A1 | A | Admiral (root) -> Issue Captain (gpt-5.6-sol requested) | not recorded as a count; the observation note's four-entry inventory is the last snapshot before it | accepted, claimed 21:32Z | faa2a00e |
| A2 | A | Issue Captain -> researcher | not recorded | accepted, later completed | 6cdad598 |
| A3 | A | Issue Captain -> second child, before the config change | four entries | refused | 6cdad598 |
| A4 | A | Issue Captain -> specifier (gpt-5.6-sol/medium) | not recorded as a count; the completed researcher had disappeared before or with it | accepted, Specifying 21:35Z | 6cdad598 |
| A5 | A | Admiral (root) -> DYD-134 captain (root-owned, necessary) | four entries | refused | 6cdad598 |
| A6 | A | Issue Captain -> SPEC reviewer, first attempt before reopen | four entries: root, captain, two completed-retained specifiers | refused; no retry, no reuse | b33e3890 |
| A7 | A | Issue Captain -> SPEC reviewer, post-reopen (gpt-5.6-sol configured) | not recorded | accepted on first attempt | c1562148 |
| A8 | A | Issue Captain -> code-writer (gpt-5.6-terra configured) | not recorded | accepted; IMPLEMENTED `9d6179ca` | 4eb7b393, 0b264e18 |
| A9 | A | Issue Captain -> hardener (gpt-5.6-terra/high requested) | not recorded | accepted; HARDENED `059c575f` | 6017a4f6, 230df449 |
| A10 | A | Issue Captain -> whole-Issue CODE reviewer | four retained entries, names not recorded | refused once; no retry, no reuse; Issue released 22:32Z, push not attempted | 7028588d |
| B1 | B | Admiral (root session) -> Issue Captain (claude-fable-5-1 requested) | not exposed by host | accepted, claimed 03:11Z | a89c67e5 |
| B2 | B | Issue Captain -> whole-Issue CODE reviewer 1 (fable requested; reported claude-fable-5-1) | not exposed by host | accepted; FAIL, two findings | fd5adf59, 28a003a5 |
| B3 | B | Issue Captain -> fix specifier (fable requested) | not exposed by host | accepted; specify amendment `e5737eda` | 1116030a |
| B4 | B | Issue Captain -> fix hardener (fable requested) | not exposed by host | accepted; this hop's two `DYD-141 fix:` commits | STATE preceding the spawn |
| B5 | B | Issue Captain -> whole-Issue CODE reviewer 2 | not exposed by host | after this hop; recorded on the Linear record, not here | - |

Between A3 and A4 the user changed the local configuration to 16 (section above). Between A6 and A7 the
task was reopened with "restart/fresh task consuming config 16" named as the blocker; whether the reopen
consumed the value is unproved. The narrative's post-reopen captain admission has no separate spawn
comment and no count on the record; the record's captain claim is A1.

Under actual budget accounting with a recorded count are only the four Host A refusals A3, A5, A6 and
A10, each at four inventory entries. Every Host A acceptance (A1, A2, A4, A7, A8, A9) is narrated on the
record with no count before it. Host B exposes no live-agent inventory, count or lifecycle control to an
agent: the Agent tool spawns, and no list or close primitive is visible to the captain. Inventory before
each Host B spawn is therefore unavailable by construction, and that missing inventory/control is the
concrete escalation result for Host B; a refusal there could be recorded only after the fact from the
spawn's own error, and none of B1-B4 was refused. Host B ran strictly serially, one child at a time, the
captain consuming each return before the next spawn. Neither host's evidence claims reload, reclamation,
backend, version, model, engine or lifecycle semantics; the value 16 remains configuration evidence only.

## Gate recovery

The implementer's captured full-run output had no result footer or `DynaDocs.Tests/coverage/results/result.json`,
so it could not establish a trustworthy exit. An initial recovery attempt began the isolated test host but
was stopped before the repository's normal duration and produced no footer; it is not gate evidence. The
single final recovery run of `py DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal` used isolated
worktree `C:/Users/User/AppData/Local/Temp/dydo-test-265437ae` at `9d6179ca` and completed with exit `0`:
`2,375` passed, `0` failed, `0` skipped, in `8 m 28 s`.

## Fix-hop gates

Run by the fix hardener on 2026-09-09 (claude-fable-5-1 requested; effective identity unproved).

- Generation gate at `e5737eda`, before fix 1: the six prompt files normalized to `w/crlf`; build exit 0,
  0 warnings; source-built `template update` reported `0 updated, 6 already current, 1 metadata-only
  document hash refresh(es), 2 source hash change(s)` and modified only the three owned `frameworkHashes`
  lines; two syncs changed nothing further. Fix 1 commits exactly that output.
- Generation gate at fix 1 (`8b4b6d86`): normalized `w/crlf`; build exit 0, 0 warnings; `template update`
  printed exactly `Template update complete: 0 updated, 6 already current.` with nothing on stderr;
  `git status --porcelain` empty after the update and after each of two syncs; `.claude/agents/issue-captain.md`,
  `.codex/agents/issue-captain.toml` and every `.agents/skills/*/agents/openai.yaml` byte-identical; all
  78 required wording checks present across the ten authored and managed prompts.
- Focused gate at `8b4b6d86`: 4 passed, 0 failed, 0 skipped, exit 0. Build: exit 0, 0 warnings.
  `dydo check`: exit 0, 0 errors, 0 warnings.
- The full run and `gap_check.py --force-run` on the final committed state run after this commit; their
  counts and exits are on the Linear record with this hop's return, not here.
