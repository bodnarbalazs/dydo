---
id: 161
area: project
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-05-04
---

# dydo issue create output cannot satisfy SummaryRule - every issue stub triggers a Missing summary paragraph warning

## Description

Commands/IssueCreateHandler.cs:159-188 (BuildBodySection) emits '## Description' immediately under the H1 in all three code paths (null body, structural-heading body, plain body). Zero content ever lands above ## Description. Services/MarkdownParser.cs:52-80 (ExtractSummaryParagraph) walks past # title, skips blank lines, then breaks at the first #-prefixed line - so when the next non-blank line is ## Description, the summary is null by construction.\n\nLive reproduction: 8 currently-flagged issues (#0151-#0158, all found-by: inquisition) all trigger 'Missing summary paragraph after title'. Older issues (#0028-#0148) only pass because Brian hand-backfilled them in commit 756bedb (cleanup-docs-check-backlog.md:18). That backfill is unsustainable.\n\nInquisitor template (Templates/mode-inquisitor.template.md:280) instructs filing as: dydo issue create --title '...' --area <a> --severity <s> --found-by inquisition. No --summary (flag doesn't exist), no --body, so the placeholder path is the default for inquisition-filed issues - dead-on-arrival warnings, every time.\n\nFix (two coordinated edits, one PR):\n1. Hard: extend IssueCreateHandler with --summary <one-line> flag (or first-paragraph promotion from body). Render as: # {title}\n\n{summary}\n\n## Description\n\n{body}. Pre-fill (One-line summary) placeholder when omitted so the file is structurally compliant.\n2. Soft: update Templates/mode-inquisitor.template.md (and equivalent passages in code-writer/reviewer mode templates that file issues) to teach the new flag. After regeneration, dydo template update propagates to active agents.\n\nAlternative path for planner to weigh: relax SummaryRule to accept first paragraph after ## Description for type: issue files. Lower-effort but loses hub preview value (HubGenerator:182-198 consumes SummaryParagraph for _index.md previews).\n\nOptional pair-commit: backfill first sentence of ## Description into a summary line for #0151-#0158 so the project starts clean.\n\nConfirmed by inquisition dydo-check-drift.md finding #3 (judge: Dexter).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)