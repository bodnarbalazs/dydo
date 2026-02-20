---
title: Audit System
type: guide
area: general
---

# Audit System

This folder contains audit logs tracking agent activity.

## Structure

- `YYYY/` - Year folders containing session logs
- `reports/` - Generated HTML visualizations

## Usage

```bash
# Generate replay visualization (all sessions)
dydo audit

# Filter to specific year
dydo audit /2025

# List available sessions
dydo audit --list

# Show details for a specific session
dydo audit --session <session-id>
```

## Log Format

Each session is stored as `yyyy-mm-dd-sessionid.json` containing all events from that session:

```json
{
  "session": "abc123",
  "agent": "Alpha",
  "human": "john",
  "started": "2025-01-15T10:23:45Z",
  "git_head": "a1b2c3d",
  "events": [
    {"ts": "...", "event": "claim", "agent": "Alpha"},
    {"ts": "...", "event": "role", "role": "docs-writer"},
    {"ts": "...", "event": "read", "path": "dydo/docs/api.md"},
    {"ts": "...", "event": "edit", "path": "src/auth.ts"},
    {"ts": "...", "event": "bash", "cmd": "npm test"}
  ]
}
```

## Event Types

- `claim` - Agent claimed identity
- `release` - Agent released identity
- `role` - Role changed
- `read` - File read
- `write` - File created
- `edit` - File modified
- `delete` - File deleted
- `bash` - Command executed
- `commit` - Git commit made
- `blocked` - Action blocked by guard

## Visualization

Run `dydo audit` to generate an interactive HTML replay. Open `reports/replay.html` in your browser to visualize agent activity as a graph.