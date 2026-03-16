---
area: reference
type: reference
---

# Audit System

Every agent action is recorded as a JSON audit trail. Sessions capture file operations, role changes, guard blocks, and a project snapshot — enabling replay, accountability, and debugging.

---

## What Gets Audited

| Event | Trigger |
|-------|---------|
| `Claim` | Agent claims identity |
| `Release` | Agent releases identity |
| `Role` | Agent sets or changes role |
| `Read` | File read |
| `Write` | File created or overwritten |
| `Edit` | File edited |
| `Delete` | File deleted |
| `Bash` | Bash command executed |
| `Commit` | Git commit made |
| `Blocked` | Guard blocks an action |

Events are logged by the guard hook on every tool call.

---

## Storage

**Location:** `dydo/_system/audit/YYYY/`

**File naming:** `yyyy-mm-dd-{sessionId}.json` — one file per agent session, sorted chronologically.

**Baselines:** `_baseline-{id}.json` — created by compaction.

**Reports:** `dydo/_system/audit/reports/replay.html` — generated visualization.

---

## Session Format

```json
{
  "session": "uuid",
  "agent": "Adele",
  "human": "alice",
  "started": "2026-03-16T10:00:00Z",
  "git_head": "abc123...",
  "snapshot": { ... },
  "events": [ ... ]
}
```

### Snapshot

Each session captures the project state at start:

```json
{
  "git_commit": "full-hash",
  "files": ["path/to/file", "..."],
  "folders": ["path/to/folder", "..."],
  "doc_links": {
    "source/file.md": ["target/file.md", "..."]
  }
}
```

### Event Fields

| Field | Present | Description |
|-------|---------|-------------|
| `ts` | Always | ISO 8601 timestamp |
| `event` | Always | Event type (Claim, Read, Blocked, etc.) |
| `path` | File ops | File path involved |
| `tool` | File ops | Tool name (read, write, edit, bash, glob, grep, agent) |
| `cmd` | Bash | Command executed |
| `exit` | Bash | Exit code |
| `role` | Role | Role set |
| `task` | Role | Task name |
| `hash` | Commit | Git commit hash |
| `msg` | Commit | Commit message |
| `agent` | Claim | Agent name |
| `reason` | Blocked | Why the action was blocked |

---

## Commands

### List sessions

```bash
dydo audit --list            # All sessions
dydo audit /2026 --list      # Filter by year
```

### Session details

```bash
dydo audit --session <id>
```

Shows agent, human, start time, git HEAD, snapshot size, and a timestamped event log.

### Replay visualization

```bash
dydo audit                   # Generate HTML replay for all sessions
dydo audit /2026             # Filter by year
```

Creates an interactive HTML timeline at `dydo/_system/audit/reports/replay.html` with playback controls, file access graphs, folder hierarchy, and doc link visualization with per-agent highlighting.

### Compact snapshots

```bash
dydo audit compact           # Compact current year
dydo audit compact 2025      # Compact specific year
```

---

## Compaction

Over time, audit files accumulate large, redundant snapshots. Compaction replaces inline snapshots with a baseline+delta scheme.

### How it works

1. **Unroll** — resolve every session to its full snapshot
2. **Find optimal baseline** — select the most common `git_head`'s snapshot
3. **Rebuild with deltas** — replace inline snapshots with `snapshot_ref` pointing to the baseline plus a delta of changes
4. **Cleanup** — remove old baseline files

### Delta format

```json
{
  "files_added": [...],
  "files_removed": [...],
  "folders_added": [...],
  "folders_removed": [...],
  "doc_links_added": { ... },
  "doc_links_removed": { ... }
}
```

Sessions whose snapshot matches the baseline exactly get a null delta. Delta chains are limited to depth 50.

### After compaction

Sessions reference the baseline instead of storing a full snapshot:

```json
{
  "snapshot_ref": {
    "type": "delta",
    "base": "baseline-id",
    "depth": 1,
    "delta": { ... }
  }
}
```

Writes are atomic (temp file + rename) for crash safety.

---

## Use Cases

- **Debugging** — "What did the agent do to this file?" — search events by path
- **Review** — session details show every action an agent took
- **History** — find when a change was introduced across sessions
- **Replay** — the HTML visualization shows agent activity as an animated timeline

---

## Related

- [Agent Lifecycle](../understand/agent-lifecycle.md)
- [CLI Commands Reference](./dydo-commands.md)
- [Configuration Reference](./configuration.md)
