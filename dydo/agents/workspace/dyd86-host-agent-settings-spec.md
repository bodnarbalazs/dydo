---
area: agents
type: specification
issue: DYD-86
base: c115e0fa4525b116691172b08e76afdac0e8292f
---

# DYD-86 Host Agent Settings

## Spec

`dydo init claude`, `dydo init codex`, and `dydo init all` configure the selected project's
host settings on both a fresh tree and `--join`; `none` leaves them absent. The setting is project
configuration intent, never evidence that a host accepted it or that a runtime capacity exists.

| Host file | Managed requirement |
| --- | --- |
| `.claude/settings.json` | `env.CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` is the JSON string `"3"`. |
| `.codex/config.toml` | The verified `[agents]` table supplies `max_depth = 3` and `max_concurrent_threads_per_session = 16`. `enabled` defaults true, so it is not written at project root. |

For each selected file, a missing verified managed key is added while unrelated content remains
intact. Repeated init/join is byte-identical after the first successful write. Claude's managed
depth satisfies the requirement only when it is a JSON string whose contents are a canonical
unsigned base-10 integer: no sign, whitespace, or leading zeroes except the string `"0"` itself,
and a decimal value of at least 3. Thus `"4"` is retained; `"03"`, `"+3"`, padded or non-numeric
strings, lower values, and non-string JSON values are actionable errors. A missing Claude value is
emitted as `"3"`. A present valid Codex depth of at least 3 and concurrency of at least 16 is
retained. A lower depth or concurrency, or `enabled = false`, is an actionable error naming the
file, key, required value, and found value; it is never silently changed or downgraded.

Malformed JSON/TOML, a non-object JSON root or `env`, incompatible managed-value types, ambiguous
TOML structure, and the above conflicts fail before any host-settings mutation. The command reports
the actionable failure and does not create or alter either selected host-settings file. Unrelated
JSON members and TOML bytes, comments, and ordering are preserved wherever their location is not
managed; the implementation must refuse an ambiguous edit rather than rewrite a TOML document.

The project `.claude/settings.json` is separate from existing personal hook wiring in
`.claude/settings.local.json`; DYD-86 neither weakens nor relies on the hook loader's malformed-JSON
fallback. Codex config is trusted-project configuration and layers root-to-current-directory, with
the closest file winning; the concurrency ceiling excludes the primary thread. V1/V2 and reload
behavior remain host qualifications, not failures. The native evidence is the official
[configuration reference](https://developers.openai.com/codex/config-reference/) and
[multi-agent guide](https://developers.openai.com/codex/multi-agent/), plus installed Codex CLI
0.153.4: `--strict-config -c agents.max_depth=3 doctor` loaded config while an invented `agents.*`
key was rejected. That proves parser placement only, not runtime enforcement. No capacity manager,
runtime probe, generated-role change, or lifecycle claim is part of this Issue; DYD-88 owns runtime
observation.

## Plan

**Pattern to copy.** `Commands/InitCommand.cs` already selects integrations in `ScaffoldProject` and
`ExecuteJoin`; its idempotent hook wiring is adjacent but separate. `Commands/SyncCommand.cs:471` and
`dydo/guides/customizing-roles.md:42` establish V1 `[agents]` vocabulary, while this Issue owns the
project document rather than generated role TOML.

**Files.**

1. `Commands/InitCommand.cs` — preflight every selected host-settings document before existing init
   or join mutations, merge only verified managed JSON/TOML settings, then write the prepared output
   after validation; use a narrow line-preserving TOML edit and fail when it cannot locate one safe
   edit.
2. `DynaDocs.Tests/Integration/InitCommandTests.cs` — prove fresh/init/join/repeat cases, unrelated
   JSON and TOML preservation, Claude's canonical-string acceptance and rejection cases,
   satisfying-value retention, each conflict/type/malformed diagnostic, and that every host
   preflight failure leaves all pre-existing init/join side-effect paths unchanged or absent:
   `dydo.json`, hooks, entry points, ignore-file, and both host-settings files. Cover `init all`
   when either its Claude or Codex target is invalid.
3. `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs` — run the built CLI with `init all` and read both
   project settings back; retain one actual join/repeat invocation to prove the shipped command path.
4. `dydo/guides/getting-started.md` — replace the DYD-86 future-tense note with the delivered
   configuration contract, concurrency-intent qualification, and Codex V2 limitation. This is a
   guide-only coordination boundary with DYD-91: do not register, template, or scaffold it here.

**Mutation order.** Determine selected integrations; read and parse every target; validate root and
managed types; resolve every conflict; construct every output; only then run existing scaffolding or
join wiring and write the prepared host-settings outputs. Validation failures therefore precede
`dydo.json`, hook, entry-point, ignore-file, or host-settings mutation. Write only changed outputs;
repeat output is byte-identical.

**Gates.** Use only `py DynaDocs.Tests/coverage/run_tests.py`: first a focused `InitCommandTests` and
`CliEndToEndTests` selection with nonzero discovery, then the full runner; the required zero-warning
build, `dydo check`, fresh built-CLI acceptance, preservation/extension, and repeated-identity checks
all pass. Record command, candidate SHA, exit, discovery, and evidence location. A fresh reviewer
receives this contract, candidate, and base SHA; native project-setting semantics warrant that review.

**Plan review.** Recommended: TOML's native configuration semantics and lossless preservation make
the parser-backed key shape and failure ordering material. Production remains sequenced after
DYD-39; V1/V2 semantics and reload behavior remain DYD-88 runtime-observation work.
