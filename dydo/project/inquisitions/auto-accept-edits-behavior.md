---
area: general
type: inquisition
---

# Auto-accept-edits behavior inquiry

## 2026-04-18 — Frank

### Claim under investigation

"Edit/Write tool calls auto-approve (skip the permission prompt) inconsistently
between projects on the same machine — LC bypasses the prompt; DynaDocs does
not." The user wants the *mechanism*, not a fix.

### Scope

- **Entry point:** Feature investigation (cross-project behavior).
- **Files investigated:**
  - `Commands/GuardCommand.cs` — guard output contract, worktree detection,
    all emit sites.
  - `DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs` — canonical tests
    for the allow-JSON behavior.
  - `dydo/project/changelog/2026/2026-04-09/fix-guard-worktree-allow.md` —
    change that introduced the worktree-conditional allow.
- **Configs inspected (redacted):**
  - `C:\Users\User\Desktop\LC\.claude\settings.local.json`
  - `C:\Users\User\Desktop\Projects\DynaDocs\.claude\settings.local.json`
  - `C:\Users\User\Desktop\Projects\DynaDocs\dydo\_system\.local\worktrees\auto-accept-edits-inquiry\.claude\settings.local.json`
    (identical to main — worktree inherits on creation)
  - `C:\Users\User\.claude\settings.json` (user scope)
  - `C:\Users\User\.claude.json` (user-scope Claude Code state, per-project entries)
- **Live probe:** ran `dydo guard` under two CWDs (worktree vs project root)
  with identical stdin.
- **Scouts dispatched:** 0 — reconnaissance only; the mechanism is small and
  concentrated in one file.
- **External reference:** Claude Code hooks docs
  (`code.claude.com/docs/en/hooks`) for the `permissionDecision` contract.

### Mechanism map

There is exactly **one** code path in this project that can auto-approve an
agent's tool call (i.e., skip Claude Code's permission prompt): the guard
emitting a specific JSON to stdout with exit code 0.

**The JSON contract** (per Claude Code hooks docs):

```json
{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}
```

`permissionDecision: "allow"` skips the prompt; `"ask"` / `"deny"` / `"defer"`
are the other legal values. Exit 0 with empty stdout does NOT auto-approve —
Claude Code falls back to its normal permission flow (settings.local.json
allow list, then prompt).

**The emit gate** — `Commands/GuardCommand.cs:73-92`:

```csharp
private const string WorktreeAllowJson =
    """{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"allow"}}""";

private const string WorktreePathMarker = "dydo/_system/.local/worktrees/";

internal static bool IsWorktreeContext() =>
    Directory.GetCurrentDirectory().Replace('\\','/').Contains(WorktreePathMarker);

private static void EmitWorktreeAllowIfNeeded()
{
    if (IsWorktreeContext())
        Console.WriteLine(WorktreeAllowJson);
}
```

The gate is **purely a CWD substring check**. If CWD contains
`dydo/_system/.local/worktrees/`, emit allow; otherwise, don't.

**Call sites that emit allow on success** (all gated by `EmitWorktreeAllowIfNeeded`):

| Handler | Line | Tool categories |
|---------|------|-----------------|
| `HandleReadOperation` | 373 | Read |
| `HandleWriteOperation` | 279 (lifted path), 304 (RBAC-pass path) | Write, Edit, Delete |
| `HandleDydoBashCommand` | 639 | Bash (dydo subcommands) |
| `AnalyzeAndCheckBashOperations` | 795 | Bash (non-dydo, after file-op analysis) |

**Handlers that never emit allow** (gap — even inside a worktree):

- `HandleSearchTool` (Glob / Grep / Agent) — passes silently on success.
- All failure/BLOCKED paths — correctly never emit allow.
- CLI-mode invocation (no stdin JSON) — same success/failure pattern.

**What governs the prompt when allow is NOT emitted** (i.e., outside
worktrees or for search tools):

1. `.claude/settings.local.json` → `permissions.allow[]` tool-pattern match.
2. If no match → Claude Code prompts the user.

So outside a worktree, the user experience is driven entirely by
settings.local.json — the guard contributes only a block/no-block decision,
not an auto-approve signal.

### Observed differences (LC vs DynaDocs, redacted)

Both files have the same structural shape: `permissions.allow[]` + a single
`hooks.PreToolUse` entry invoking `dydo guard`. Differences:

| Aspect | LC | DynaDocs |
|--------|------|----------|
| `hooks.PreToolUse.matcher` | `Edit|Write|Read|Bash|Glob|Grep` | `Edit|Write|Read|Bash|Glob|Grep|Agent|EnterPlanMode|ExitPlanMode` |
| `permissions.allow[]` coverage | `Write(**)`, `Write(~/**)`, `Write(/c/Users/User/Desktop/LC/**)`, plus backslash and forward-slash variants; same for `Read(...)`. Bash allowlist includes `dotnet`, `npm`, `npx`, `uv`, `pytest`, a few project scripts. | `Write(**)`, `Write(~/**)`, `Write(/c/Users/User/Desktop/Projects/DynaDocs/**)` + variants; same for `Read(...)`. Bash allowlist includes `find`, `ls`, `grep`, `dotnet`, `dydo`, project test scripts. |
| Explicit `Edit(...)` rule | absent | absent |
| `enableAllProjectMcpServers` | `true` | not set |
| MCP server entry (`aspire-dashboard` with HTTP URL and API key) | present | not present |
| `permissionMode` / `defaultMode` / `acceptEdits` flag | absent | absent |

Both projects' `~/.claude.json` entries show `hasTrustDialogAccepted: true`,
`allowedTools: []`, no per-project permission overrides. User-scope
`~/.claude/settings.json` contains only `enabledPlugins`, `autoUpdatesChannel`,
`effortLevel` — no permission config.

**Worktree state at time of inquiry:**

- LC: `dydo/_system/.local/worktrees/` directory exists but is **empty** (no
  active worktrees on disk). `~/.claude.json`'s `githubRepoPaths` records
  11 LC worktree paths that Claude Code has opened historically — all
  currently cleaned up.
- DynaDocs: 10+ active worktrees on disk (coverage/inquisition/fix-*).
  `githubRepoPaths` records 60+ DynaDocs worktree paths (historical).

**Live probe** (same stdin JSON, different CWDs, same dydo binary):

```
cwd = .../worktrees/auto-accept-edits-inquiry/  → stdout: {"hookSpecificOutput":...,"permissionDecision":"allow"}  exit 0
cwd = C:\Users\User\Desktop\Projects\DynaDocs\  → stdout: (empty)                                                  exit 0
```

Confirms the mechanism: only CWD-inside-worktree produces the auto-approve
signal.

### Most likely explanation (ranked)

**1. Where Claude Code was started, not which project it is.** (high
confidence)

The code mechanism is project-agnostic — same binary, same gate, same JSON.
It treats "inside a worktree" differently, period. The observed
LC-vs-DynaDocs difference is almost certainly a byproduct of **CWD at
Claude Code startup**:

- Dispatched agents in both projects run inside a worktree (dispatch creates
  the worktree and launches a new terminal there) — the guard emits allow,
  no prompt.
- The *main / human-driven* session typically starts at the project root.
  At LC's project root, `dydo/_system/.local/worktrees/` is empty today,
  so the user would *also* see prompts there — but LC has been in
  maintenance for a while; the user's recent session that felt "pleasant"
  was likely a dispatched agent terminal or a session started inside a
  coverage-slice worktree back when those existed.
- DynaDocs is in active development — the user frequently drives a session
  from the project root, which hits the no-emit branch and prompts.

The "same agents, same kinds of edits" observation is consistent with this:
the *dispatched* agents (who do most edits) never prompt in either project;
the user only notices prompts when they drive the main-root session, which
DynaDocs does more often.

**2. `Write(**)` in allow list is not carrying the load.** (medium
confidence)

Both projects have `Write(**)` (and `Read(**)`) in `permissions.allow`.
If that rule actually auto-approved every Write/Edit by itself, both
projects would be silent at the project root. The fact that DynaDocs
still prompts suggests either (a) the `**` pattern doesn't expand as the
user expects on Windows paths, (b) the Edit tool doesn't match a `Write(...)`
rule (distinct tool namespace), or (c) the guard's `dydo guard` hook runs
first and, on not emitting allow, Claude Code *still* falls to its own
prompt rather than the settings allow list. I couldn't cleanly test this
without driving Claude Code UI. But this is secondary — the mechanism in
*our* code is #1.

**3. Settings drift of MCP / hook matcher.** (low confidence, probably
unrelated to the prompt question)

LC's `enableAllProjectMcpServers: true` plus the `aspire-dashboard` MCP
server entry affect MCP tool loading. DynaDocs hooks `Agent|EnterPlanMode|
ExitPlanMode` where LC does not — meaning the Agent sub-agent tool passes
through `dydo guard` in DynaDocs and gets treated as a search tool
(`HandleSearchTool`), which never emits allow. So DynaDocs main sessions
that use `Agent` (sub-agent dispatch) would prompt; LC main sessions would
not (hook skipped → normal permission flow). This could make the Agent-tool
path more visible as a prompt in DynaDocs specifically. Low confidence
because the user's observation is about Edit/Write, not Agent.

### Is it intentional?

**Partially — and surfacing a gap.**

- The worktree-conditional allow was intentional (see
  `dydo/project/changelog/2026/2026-04-09/fix-guard-worktree-allow.md`).
  The rationale: inside a worktree, settings.local.json path patterns
  typically don't resolve correctly against the worktree-rewritten path
  (the `dydo/_system/.local/worktrees/{id}/...` absolute path doesn't
  match `Write(/c/Users/User/Desktop/Projects/DynaDocs/**)`), so without
  the guard's explicit allow, agents would hit prompts. Worktree allow
  closed that gap.
- The side-effect — "only agents in worktrees get auto-approved; main
  sessions at project root still prompt" — appears to be a side-effect
  rather than a designed UX choice. The changelog doesn't mention the
  non-worktree path.
- `HandleSearchTool` missing the emit is an acknowledged pre-existing gap
  (flagged in the same PR's review by Dexter but deferred).

### Recommendations

Presenting tradeoffs only; no implementation.

**Option A — accept as-is, document it.**

Add a short note to `dydo/understand/guard-system.md` explaining: "The guard
auto-approves tool calls (skips Claude Code's prompt) only inside a dispatch
worktree; at the project root you will still see prompts unless
`settings.local.json` whitelists the pattern." Cheapest; preserves current
security surface.

**Option B — emit allow unconditionally on guard success.**

Drop `IsWorktreeContext()` gating; emit the allow JSON from every success
branch (Read, Write, Bash, plus the currently-missing Search). This makes
behavior consistent between project root and worktrees.

- Security tradeoff: the guard is already the authoritative RBAC/off-limits
  check — its decision supersedes Claude Code's prompt. Making it always
  emit allow when it already says "exit 0" removes a *second* prompt that
  wasn't adding security (only adding friction). It does remove a human
  "are you sure" step on destructive writes at project root, which some
  users rely on as a psychological checkpoint.
- This is the smallest, most consistent change if the goal is uniform UX.

**Option C — close the Search-tool gap but keep worktree-only gating.**

Add `EmitWorktreeAllowIfNeeded()` to `HandleSearchTool` (after the off-limits
and identity/role checks). Minimal change; fixes a tiny within-worktree
inconsistency without altering the main-session behavior.

**Option D — make the gate opt-in via dydo.json.**

Add a `guard.auto_approve_on_success: true|false` (or similar) setting to
`dydo.json`. Project chooses. Most flexible, most config surface; heaviest
change.

Recommend starting with Option A (document) and separately considering
Option B (if the team's answer is "yes, guard is the single source of
truth"). Both cheap.

### Findings

Recorded as obvious findings only — no hypotheses needed testing.

#### 1. `HandleSearchTool` never emits allow JSON, even inside a worktree

- **Category:** antipattern / inconsistency
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Commands/GuardCommand.cs:400-437`. `HandleReadOperation`,
  `HandleWriteOperation`, `HandleDydoBashCommand`, and
  `AnalyzeAndCheckBashOperations` all call `EmitWorktreeAllowIfNeeded()` at
  their success returns. `HandleSearchTool` ends with `return
  ExitCodes.Success` (line 436) with no emit. Result: Glob/Grep/Agent tools
  prompt the user even in worktree context. Same gap flagged by reviewer
  on 2026-04-09 PR (`fix-guard-worktree-allow`) as "out-of-scope / pre-
  existing" and deferred.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/GuardCommand.cs` (lines 73-92, 239-307,
  345-376, 400-437, 591-642, 714-798); `dydo/project/changelog/2026/
  2026-04-09/fix-guard-worktree-allow.md` (full review block).
- **Independent verification:** Re-read every emit site cited.
  `EmitWorktreeAllowIfNeeded()` calls present at lines 279, 304, 373, 639,
  795 — exactly the four handlers Frank named, and absent from
  `HandleSearchTool` (line 436 returns `ExitCodes.Success` with nothing
  written to stdout). Cross-referenced the 2026-04-09 review note: Dexter
  flagged both `HandleSearchTool` and `AnalyzeAndCheckBashOperations` as
  out-of-scope gaps. The latter has since been closed (line 795 emits);
  `HandleSearchTool` remains uncovered. Confirmed via
  `GuardWorktreeAllowTests.cs` that no existing test asserts allow
  emission for Glob/Grep/Agent.
- **Alternative explanations considered:** Could be intentional —
  Search tools never write, so a missing auto-approve doesn't risk
  bypassing safety. But the docs offer no rationale, the four sister
  handlers (including the read-only `HandleReadOperation`) all emit, and
  Dexter's review labelled it a "gap", not an intentional carve-out.
- **Issue:** #0099

#### 2. `IsWorktreeContext` uses unanchored substring match

- **Category:** bug (theoretical; low impact)
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Commands/GuardCommand.cs:80-86` —
  `cwd.Contains("dydo/_system/.local/worktrees/")`. Any directory that
  happens to include that substring anywhere — e.g. a user project named
  `my-dydo/_system/.local/worktrees-notes/` or a backup directory — would
  be treated as a worktree and get auto-approve. Probability in practice:
  near zero, but the check does not anchor to the project root or verify
  the worktree marker files (`.worktree`, `.worktree-path`). Fix would be
  to combine with `PathUtils.GetMainProjectRoot(...)` (already used in
  `ResolveWorktreePath`) or a `.worktree` marker probe.
- **Judge ruling:** CONFIRMED
- **Files examined:** `Commands/GuardCommand.cs` (lines 76, 80-86, 1027-1039);
  `DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs` (lines 192-232,
  the four `IsWorktreeContext_*` cases).
- **Independent verification:** Re-read the function. It is literally
  `Directory.GetCurrentDirectory().Replace('\\','/').Contains("dydo/_system/.local/worktrees/")`
  with no anchoring, no project-root check, and no marker-file probe.
  `ResolveWorktreePath` (line 1027-1039) already uses
  `PathUtils.GetMainProjectRoot(...)` for relative-path resolution, so
  the safer primitive is available in the same file. The existing tests
  cover the happy paths (worktree, normal CWD, nested, backslash) but no
  test covers a path that contains the marker substring without being a
  real worktree (e.g., `worktrees-notes/`, `worktrees-archive/`).
- **Alternative explanations considered:** A stricter check would cost
  filesystem I/O on every guard invocation (the guard runs on every tool
  call). That's a real tradeoff and could justify the current approach
  in principle — but no comment or doc records the decision, so it reads
  as oversight rather than deliberate. The bug is theoretical at low
  severity, matching Frank's framing.
- **Issue:** #0100

#### 3. Rationale for worktree-only gating is undocumented

- **Category:** missing-documentation
- **Severity:** low
- **Type:** obvious
- **Evidence:** Searched `dydo/understand/guard-system.md` and
  `dydo/understand/worktree-system.md` for a description of the auto-approve
  emission and its gating. Neither file explains the `permissionDecision:
  "allow"` contract or why worktree contexts get it and project-root does
  not. The only trace is in the 2026-04-09 changelog. Users (including
  this inquirer) discover the mechanism only by code-diving.
- **Judge ruling:** CONFIRMED
- **Files examined:** `dydo/understand/guard-system.md` (full grep for
  `permissionDecision|hookSpecificOutput|auto.?approve|allow.*JSON`),
  `dydo/reference/guardrails.md`, `dydo/understand/architecture.md`,
  `dydo/_system/.local/worktrees/auto-accept-edits-inquiry/dydo/project/changelog/2026/2026-04-09/fix-guard-worktree-allow.md`.
  Also confirmed via `Glob **/worktree-system.md` that no such file
  exists in the project (Frank's reference to it is slightly inaccurate
  but does not change the substantive claim).
- **Independent verification:** `Grep` over `dydo/` for
  `permissionDecision|hookSpecificOutput|EmitWorktreeAllow|WorktreeAllowJson`
  returns three changelog files only — no user-facing doc. Re-read
  `guard-system.md` end-to-end: it covers blocking guardrails, staged
  access, off-limits, and audit, but never references the allow
  envelope nor explains why prompts disappear inside worktrees.
  `architecture.md`'s "Worktree Dispatch" section describes lifecycle
  and markers but not the prompt behaviour.
- **Alternative explanations considered:** Could be deliberately
  undocumented because the auto-approve is meant to be invisible
  plumbing rather than a feature. But the user-visible side-effect
  (prompts at root, silence in worktree) is a feature/UX concern, and
  is exactly what triggered this inquisition — so the silence is doing
  harm, not avoiding it.
- **Issue:** #0101

### Hypotheses not reproduced

- *Settings.local.json `Write(**)` actually auto-approves DynaDocs edits on
  its own.* Could not drive Claude Code UI to verify. If true, the user's
  observation would not exist — so this is implicitly disproven by the
  observation itself. Leaving as inconclusive rather than confirmed.

### Confidence: medium

- **Covered thoroughly:** the guard's output contract, every emit site,
  the CWD gate, configuration diff between the two projects (redacted).
- **Covered shallowly:** Claude Code's own permission-matching behavior for
  `Write(**)` / `Edit(...)` patterns — docs were not explicit; couldn't
  observe the Claude Code UI prompting dynamics directly. This is the main
  uncertainty. If `Write(**)` actually did auto-approve everywhere, the
  observation wouldn't arise — so either it doesn't, or there's a subtle
  matcher quirk (path normalization, tool-namespace, etc.) that matters.
- **Not examined:** any differences in Claude Code *version* between LC and
  DynaDocs terminals (user said same, trusting that). `%LOCALAPPDATA%\claude\`
  on Windows — checked via `$LOCALAPPDATA` expansion, returned empty;
  assumed not present or not relevant.

### Judge verdict (2026-04-18 — Emma)

All three findings CONFIRMED. Issues #0099 (HandleSearchTool gap),
#0100 (unanchored substring), #0101 (undocumented gating) filed.

On the headline question — *does the LC-vs-DynaDocs prompt
inconsistency the user observed warrant a fix task?* — my reading is
**not as a behaviour bug, but as a documentation-and-coverage gap**:

- The mechanism map confirms the difference is purely a function of
  *where Claude Code was started* (worktree vs project root), not a
  per-project configuration drift. Same binary, same gate, same JSON.
  There is nothing wrong with the code emitting allow inside worktrees;
  that's the deliberate fix from 2026-04-09.
- What's missing is (a) the user's mental model — addressed by issue
  #0101 (document the gating) — and (b) the Search-tool gap that makes
  even the worktree behaviour inconsistent across handlers — addressed
  by #0099.
- Frank's Option A (document) and Option C (close the search-tool gap
  without changing main-session behaviour) together cover the
  headline observation cheaply and conservatively. Option B
  (unconditional emit) is a real-but-larger UX call that I would not
  recommend folding into this inquisition's follow-up — it changes the
  security posture for project-root sessions, and that decision
  deserves its own discussion.

Recommendation to the human: treat #0099 + #0101 as the immediate
follow-up. Defer the Option B / Option D conversation as a separate
design question.
