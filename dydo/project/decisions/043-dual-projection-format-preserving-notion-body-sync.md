---
area: backend
type: decision
status: accepted
date: 2026-08-17
participants: [balazs, Codex]
---

# 043 — Dual-Projection, Format-Preserving Notion Body Sync

Authored repo Markdown and Notion's Markdown echo are two representations of one document, not two
strings that can safely share one merge base. Body sync therefore stores one base per representation,
detects changes within each representation, and translates genuine edits through a format-preserving
Markdown syntax-tree alignment. It never uses normalization to overwrite canonical content.

This completes DR 025's uniformly bidirectional authored-content contract and supersedes DR 035 §3's
single-normalized-base approach for PM-spine bodies. It resolves issue 0309 without a temporary
quarantine or a one-way ownership rule.

## Context

The old spine path writes bodies through `NotionBlockConverter`, stores only the authored repo body,
and compares both sides after applying the converter again. That is safe only when the projection is a
strict fixed point. The ns-8 implementation skipped its planned canonical snapshot/hash and relied on
a current-corpus fixed-point sweep instead. The suite even records a known body that changes across two
normalizations under the assumption that no synced record contains it. A later Slice invalidated that
closed-world assumption: the watchdog classified its own lossy echo as an external edit, stripped
formatting, merged duplicated regions, and serialized board-shaped collateral into the tracked file.

Moving the spine to Notion's native Markdown API removes the in-house block converter but does not make
the channel lossless. DR 035's live evidence records leading-H1 removal, escape insertion, blank-line
collapse, and indentation changes. Comparing local bytes to that dialect—even after increasingly broad
normalization—cannot distinguish channel drift from authorship and will eventually repeat the failure.

## Decision

### 1. Store a dual body base

Snapshot v2 is discriminated per object (so migrated v2 and unresolved v1 entries may coexist) and stores:

- `localBody`: the exact authored body bytes at the last completed sync;
- `externalBody`: the exact stable-cleaned Markdown projection read from Notion at that same sync;
- the external page id and existing field base;
- a schema version and any durable pending-write intent.

Line endings may be canonicalized at the filesystem boundary, and documented nondeterministic export
artifacts such as expiring URL signatures may be removed from `externalBody`. No whitespace, heading,
list, escape, or formatting normalization crosses from one representation into the other.

Change detection is representation-local:

- repo changed: current repo body versus `localBody`;
- Notion changed: current stable-cleaned Notion Markdown versus `externalBody`.

A daemon's own lossy echo is therefore the stored external base, not a future external edit.

### 2. Use Notion's native Markdown API for every spine body

Spine body create, read, and update use the already-landed native Markdown client surface from DR 035.
Properties remain on the existing database-property path. `NotionBlockConverter`, block append/delete,
and `FromBlocks(ToBlocks(body))` are retired as spine body transport and comparison machinery.

After every successful body write, the adapter immediately reads the page Markdown and returns that
observed projection as the write receipt. The neutral read contract distinguishes `Complete` from
`Truncated`; a truncated export is unavailable evidence, never a shortened body. The runner advances
`localBody` to the exact body it wrote and `externalBody` to a complete receipt—not to a predicted
normalization.

Property-only upserts explicitly carry no body-write operation. For a page with child pages, a native
Markdown update uses `allow_deleting_content:false` and re-appends Notion's exported `<page>` tags to the
wire body; the receipt cleaner strips those structural tags so they enter neither base nor canonical file.

### 3. Translate real edits with a format-preserving syntax-tree patch

When Notion genuinely changed, parse `localBody`, `externalBody`, the current repo body, and the current
Notion body into source-spanned Markdown syntax trees. Align the two base trees by semantic node identity
and order, then express local and external changes as operations on that shared alignment.

- Unchanged local source spans—including blank lines, heading spelling, list markers, escapes, inline
  markup, and surrounding frontmatter—are copied byte-for-byte from the current repo file.
- A uniquely mapped external insertion, deletion, or modification is grafted at the smallest unambiguous
  node/span. New syntax with no local spelling is rendered deterministically as repo Markdown.
- Disjoint local and external operations compose automatically.
- Competing or ambiguously mapped operations are a genuine conflict and go to the existing spine shadow;
  the canonical file and base remain unchanged until resolution.

Structured conflicts render the complete local/external candidates inside the existing endpoint merge
sentinels. Thus every unresolved shadow remains marker-bearing and every marker-free shadow remains an
unambiguous human resolution; no second sidecar state machine is introduced.

The old raw `ThreeWayTextMerge` is not used for projected bodies. Normalization remains useful only for
semantic alignment/equivalence; normalized text is never persisted and is never the sole merge base.

### 4. Keep body and frontmatter decisions independent

A body-only Notion edit rewrites only the body span. It must leave the complete frontmatter byte-identical,
including field order, comments, quoting, and repo-only keys. A field-only edit keeps the body
byte-identical. When both changed, the field merge and body projection merge produce one composed file;
neither side is allowed to drag unchanged representation details from the other channel.

### 5. Make writes crash-safe

Before an external body mutation, durably record a pending operation containing its explicit kind
(`Create`, `Update`, or `Resolution`), the prior dual base, the intended local body, nullable external id,
and a UUID operation identity. A successful write plus complete read-back receipt atomically replaces
the dual base and clears the pending record.

Every provisioned PM data source has an engine-reserved `dydo-write-id` rich-text property. Its model
definition carries the existing `hidden` presentation hint for newly configured views, but correctness
does not depend on visibility: reused views may show the column until separately reconfigured. It is
excluded from canonical fields and written from the durable operation identity on every body mutation.
For creates, the identity is included in the initial page request. After an ambiguous response or a
process restart, dydo queries the data source and adopts only the single unarchived page carrying the
exact pending UUID. Zero matches permits one retry; more than one is an ambiguity and shadows. Page
title is never a recovery key. This gives a create an identity before Notion assigns its page id and
makes a landed create recoverable even if the process dies before recording that id.

Before projected reconciliation or creation, provisioning adds an absent `dydo-write-id` and then reads
the live schema back. If the property is not exactly `rich_text` (including a pre-existing same-name
column of another type), sync fails closed before any canonical or page mutation; it never attempts an
unsafe retype. Rollback leaves this inert engine column in place rather than destructively changing a
user's remote schema.

On restart, first recover a nullable external id by the pending UUID when the operation is a create, then
read the current external body. If it is semantically the intended write and the repo still matches the
intent, adopt the observed projection and complete the receipt. If content diverged, the export is
truncated, the UUID is duplicated, or either side moved, reconcile from the recorded prior bases;
ambiguity goes to shadow. A crash after Notion accepted a write can therefore neither manufacture an
external author, duplicate a row, nor silently advance an unobserved base.

### 6. Migrate legacy snapshots without choosing a winner silently

Snapshot v1 is upgraded per object on first read, without writing either side:

- if repo and Notion are semantically equivalent to the legacy base/known legacy echo, adopt the current
  repo bytes as `localBody` and current Notion Markdown as `externalBody`;
- if one side has a uniquely provable change, carry it through the new projection merge;
- if both changed or the legacy state is ambiguous, create a migration conflict shadow and leave the
  canonical file, Notion page, and legacy base untouched.

This is permanent correctness behavior for unknowable pre-upgrade state, not a temporary body-sync mode.

### 7. Keep conflict safety permanent

Shadowing is reserved for genuine overlap, ambiguous projection, truncated export, or unprovable legacy
migration—not for all external edits. No result containing conflict markers or an uncertain body mapping
may be written to a canonical file or pushed to Notion. Resolved-shadow promotion creates a new dual base
from the promoted local bytes and a confirmed external write receipt. Promotion first persists a durable
resolution intent against the current external projection; it never pre-advances the base. The shadow is
removed only after the resolution push, read-back receipt, and dual-base commit succeed.

## Acceptance

The delivery Sprint must prove all of the following:

1. The sanitized slice-11 fixture survives existing-file → local edit → watchdog push → real Notion
   read → watchdog tick with the entire file byte-identical and action `None`.
2. A genuine Notion body edit imports exactly once; every untouched body span and all frontmatter remain
   byte-identical. The next tick is `None`.
3. Disjoint two-sided edits merge once; overlapping and ambiguous edits shadow without changing either
   canonical side or advancing the base.
4. Notion-originated creation remains pristine, then receives a complete dual base so its next tick is
   quiet.
5. Legacy snapshot migration covers safe adoption, one-sided change, ambiguity, and interrupted migration.
6. A write/read-back crash and restart cannot misclassify the daemon's write or lose a concurrent edit.
7. Formatting-rich property-based fixtures exercise headings, blank lines, inline formatting, nested
   lists, tables, quotes, code, escapes, and repeated sections; mutations prove semantic edits are not
   over-normalized away.
8. The exact full-sync and delta/watchdog paths pass offline, followed by isolated scratch-page live tests
   against real Notion and the full project gates.

## Honest boundary

Notion cannot transmit distinctions it does not expose. A formatting-only gesture that Notion itself
erases before export is unobservable and cannot be imported. The guarantee is therefore: dydo never
mistakes channel loss for authorship, never damages untouched repo bytes, faithfully imports every
observable Notion edit, and surfaces ambiguity instead of inventing certainty.
