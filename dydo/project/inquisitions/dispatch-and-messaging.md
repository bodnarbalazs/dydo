# Inquisition: Dispatch and Messaging System

## 2026-04-03 — Brian

### Scope
- **Entry point:** Area investigation — DispatchService, MessageService, InboxService, QueueService, and all supporting services
- **Files investigated:**
  - Services/DispatchService.cs (772 lines)
  - Services/MessageService.cs (182 lines)
  - Services/InboxService.cs (199 lines)
  - Services/QueueService.cs (517 lines)
  - Services/InboxItemParser.cs (205 lines)
  - Services/InboxMetadataReader.cs (75 lines)
  - Services/MessageFinder.cs (96 lines)
  - Services/WatchdogService.cs (371 lines)
  - Services/MarkerStore.cs (255 lines)
  - Services/AgentSelector.cs (97 lines)
  - Services/ProcessUtils.cs
  - Services/WindowsTerminalLauncher.cs
  - Commands/WaitCommand.cs (171 lines)
  - Models/InboxItem.cs, QueueEntry.cs, QueueActiveEntry.cs, QueueResult.cs
  - DynaDocs.Tests/Services/InboxServiceTests.cs
  - DynaDocs.Tests/Services/InboxItemParserTests.cs
- **Docs cross-checked:** dydo/understand/dispatch-and-messaging.md, dydo/understand/architecture.md
- **Scouts dispatched:** 4 reviewers (Charlie, Grace, Henry, Iris) — reports pending at time of writing

### Findings

#### 1. Dead code: MarkerStore is an unused duplicate of AgentRegistry marker logic
- **Category:** dead-code / antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** `Services/MarkerStore.cs` contains ~150 lines of Wait marker, ReplyPending marker, and Dispatch marker code that is **identical** to `Services/AgentRegistry.cs` lines 955-1180. `MarkerStore` is only referenced in `DynaDocs.Tests/Services/MarkerStoreTests.cs` — no production code uses it. It appears to be an incomplete extraction (class takes a `Func<string, string> getAgentWorkspace` for testability) that was never wired into the rest of the codebase.
  - `MarkerStore.CreateReplyPendingMarker` = `AgentRegistry.CreateReplyPendingMarker` (line-for-line identical)
  - `MarkerStore.GetReplyPendingMarkers` = `AgentRegistry.GetReplyPendingMarkers`
  - `MarkerStore.RemoveReplyPendingMarker` = `AgentRegistry.RemoveReplyPendingMarker`
  - Same for all Wait and Dispatch marker methods
- **Judge ruling:** CONFIRMED — Issue #0004. Zero production references; only MarkerStore.cs and MarkerStoreTests.cs. AgentRegistry has identical methods at lines 955-1180.

#### 2. Dead code: QueueService.TryEnqueue is superseded
- **Category:** dead-code
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Services/QueueService.cs:193` — `TryEnqueue` was replaced by `TryAcquireOrEnqueue` (per `dydo/project/changelog/2026/2026-03-27/fix-queue-defaults-merge.md`). No production code calls `TryEnqueue`; it is only referenced in test files (`QueueServiceTests.cs`, `DispatchQueueTests.cs`, `QueueCommandTests.cs`). The replacement `TryAcquireOrEnqueue` uses file locking (`WithQueueLock`); the leftover `TryEnqueue` does not, making it racy if ever used concurrently.
- **Judge ruling:** CONFIRMED — Issue #0005. TryEnqueue is only called from test files (QueueServiceTests, DispatchQueueTests, QueueCommandTests). Production code uses TryAcquireOrEnqueue with proper file locking.

#### 3. Duplicated file-lock pattern across DispatchService and QueueService
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** Two nearly identical lock implementations:
  - `Services/DispatchService.cs:650-715` — `WithWorktreeLock` + `TryRemoveStaleLock`
  - `Services/QueueService.cs:452-516` — `WithQueueLock` + `TryRemoveStaleLock`
  
  Both use the same algorithm: `FileMode.CreateNew` for atomic creation, write PID JSON, retry loop with 30 attempts / 1s delay, stale-lock detection via PID liveness check. The only difference is the method name. Per coding standards "Rule of Three" this is already at 2 copies; a shared `FileLock` utility would eliminate ~65 lines of duplicated logic.
- **Judge ruling:** CONFIRMED — Issue #0006. Read both implementations side-by-side. Same algorithm: FileMode.CreateNew, PID JSON, 30 retries at 1s, stale-lock via PID liveness. Only method name and error message differ.

#### 4. Five separate hand-rolled YAML frontmatter parsers
- **Category:** antipattern
- **Severity:** medium
- **Type:** obvious
- **Evidence:** Five different implementations of "read YAML frontmatter and extract key-value pairs":
  1. `Services/InboxItemParser.cs:101-123` — Dictionary-based dispatch, dedicated `YamlParseState` class, `StringFieldSetters` dictionary. Most elaborate.
  2. `Services/DispatchService.cs:737-771` — `ParseInboxItemOrigin`, inline switch for `origin`/`from` fields only.
  3. `Services/InboxMetadataReader.cs:36-73` — `ReadFrontmatterField`, inline loop extracting a single named field plus `received` for ordering.
  4. `Services/WatchdogService.cs:331-370` — `ParseStateForWatchdog`, inline switch for `agent`/`status`/`auto-close`/`window-id`.
  5. `Services/MessageFinder.cs:37-86` — `ParseMessageFile`, inline switch for `type`/`from`/`subject`.
  
  All share the same structure: find `---` delimiters, split lines on `\n`, split each line on first `:`. A single shared frontmatter parser would eliminate ~100 lines of redundancy and prevent divergent parsing behavior.
- **Judge ruling:** CONFIRMED — Issue #0007. All five share: find `---`, split `\n`, split on first `:`, switch/dictionary on key. Five independent copies of the same parsing logic.

#### 5. Doc/code mismatch: worktree inheritance behavior
- **Category:** doc-discrepancy
- **Severity:** medium
- **Type:** obvious
- **Evidence:**
  - **Doc says** (`dydo/understand/dispatch-and-messaging.md:63`): "If the sender is already in a worktree, the child inherits the same worktree instead of creating a new one (a nudge is emitted)."
  - **Code does** (`Services/DispatchService.cs:145-171`): The `WriteAndLaunch` method has four branches:
    1. `senderWorktreeId != null && !needsMerge && worktree` → **Creates a child worktree** (`SetupChildWorktree`)
    2. `senderWorktreeId != null && !needsMerge` (no `--worktree` flag) → **Inherits parent worktree** (`InheritWorktree`)
    3. `senderWorktreeId != null && needsMerge` → Merge dispatch
    4. else → Normal worktree setup
  
  The doc describes only branch 2 (inheritance) and omits branch 1 (child worktree creation when `--worktree` is explicitly set from within a worktree). The doc also mentions a nudge being emitted — no nudge is present in the current code for inheritance. Additionally, the doc does not mention the merge dispatch path at all.
- **Judge ruling:** CONFIRMED — Issue #0008. Code has four worktree branches; doc describes only inheritance. `--worktree` from within a worktree creates a child worktree (branch 1), not inheritance. No nudge emitted. Merge dispatch path undocumented.

#### 6. MessageFinder orders messages by File.GetCreationTimeUtc
- **Category:** bug
- **Severity:** low
- **Type:** obvious
- **Evidence:** `Services/MessageFinder.cs:13` — `Directory.GetFiles(inboxPath, "*-msg-*.md").OrderBy(f => File.GetCreationTimeUtc(f))`. File creation time is unreliable across file copies, moves, and OS-level operations. Each message file contains a `received` timestamp in the YAML frontmatter (`received: 2026-04-03T14:50:32Z`) that represents the actual send time. `InboxService.ExecuteShow` correctly orders by `item.Received` (line 58), but `MessageFinder` — which is used by `WaitCommand` to find messages — relies on filesystem metadata instead. If a file is copied or restored from backup, creation time could be wrong, causing messages to be consumed out of order.
- **Judge ruling:** CONFIRMED — Issue #0009. MessageFinder.cs:13 uses File.GetCreationTimeUtc; InboxService.ExecuteShow uses item.Received. Frontmatter timestamps are authoritative; filesystem metadata is not.

#### 7. Excessive parameter counts on DispatchService methods
- **Category:** antipattern
- **Severity:** low
- **Type:** obvious
- **Evidence:**
  - `Services/DispatchService.cs:9` — `Execute(...)` has 12 parameters
  - `Services/DispatchService.cs:124` — `WriteAndLaunch(...)` has 17 parameters (plus 1 optional)
  - `Services/DispatchService.cs:448` — `LaunchTerminalIfNeeded(...)` has 9 parameters (plus 2 optional)
  
  Per C# conventions and clean code principles, methods with more than ~5 parameters are candidates for parameter objects. A `DispatchOptions` record could consolidate `noLaunch`, `useTab`, `useNewWindow`, `autoClose`, `wait`, `worktree`, `queue`, `escalate`, `files` into a single typed object, improving readability and making future parameter additions non-breaking.
- **Judge ruling:** CONFIRMED — Issue #0010. Execute: 12 params, WriteAndLaunch: 17+1 optional, LaunchTerminalIfNeeded: 9+2 optional. A DispatchOptions record would consolidate the boolean/string flag sprawl.

### Hypotheses Not Reproduced

- **QueueService `--no-launch --queue` deadlock** — Hypothesized that PID 0 from noLaunch would cause the watchdog to permanently hold the queue. Disproved: `ProcessUtils.IsProcessRunning` returns false for PID <= 0 (line 23), and the watchdog's agent state check (`AgentStatus.Dispatched`) provides a safety net (WatchdogService.cs:237).

### Workflow Friction Noted
- The guard blocked all tool calls until waits were registered for dispatched scouts — this is correct enforcement but created a friction point where I had to register 4 background waits before continuing any investigation.
- `dydo agent claim auto` assigned "Brian" when the user invoked "Frank" — Frank was in dispatched status, so auto-claim chose the next available agent. Expected behavior but may confuse users expecting a specific agent name.

### Confidence: high

All seven target service files were read in full and cross-referenced. Models, commands, tests, and documentation were also reviewed. The YAML duplication finding (4 instances) was verified by reading all five parsers. The MarkerStore duplication was verified line-by-line. The doc mismatch was verified by tracing all four worktree code paths in `WriteAndLaunch`.

Areas not examined: TerminalLauncher platform-specific logic (macOS/Linux launchers), full integration test coverage analysis, AgentSelector edge cases, RoleConstraintEvaluator internals.
