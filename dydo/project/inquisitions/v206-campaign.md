---
area: project
type: inquisition
---

# Inquisition: v2.0.6 Campaign (cross-campaign QA gate)

Campaign-end QA over everything landed in `v2.0.5..HEAD` on `master` — balazs-requested background QA, zero-human-interaction. The four bodies of work (codex-hardening adoption sprint, the 0250 impersonation fix, the DR-035 docs-body chain, the notion reset command) were each slice-reviewed and sprint-audited individually; this sweep's value is the **cross-campaign lens** — seams between the landings, regressions one introduced against another's assumptions, and blind spots all the per-slice reviews structurally shared.

## 2026-07-09 — Leo (co-thinker)

### Scope

- **Entry point:** Campaign-end inquisition (assess-only) dispatched by Adele, task `v206-campaign-inquisition-2`.
- **Range:** `v2.0.5..HEAD` (source + tests). Commits: codex-hardening (`f7e87516`, `de0d63f6`, `dc1333b9`); 0250 fix (`7805e004`); DR-035 chain (`85674a76`, `c2aeff8a`, `a2fc9218`); notion reset (`6d985884`). PM/docs commits in scope for the doc-drift lens only.
- **Method:** `inquisition` workflow — 5-lens sweep (correctness, coverage, security, dead code, doc drift) fanned across inquisitor agents, every finding adversarially verified before reporting, then synthesis. 28 agents, ~1.4M subagent tokens.
- **Constraints honored:** assess-only — no fixes, no staging, no source commits. Test-writers used only to verify hypotheses. Known-open items excluded as findings: 0233, 0253, 0254, the DR-035 convergence gap, 0249 residue.

### Verdict

**Gate: FAIL.** 23 findings confirmed, 0 plausible-only, 0 refuted — **2 high, 6 medium, 15 low**. The headline is a **cross-campaign regression the two identity landings created together**: the 0250 fix closed the impersonation hole on the file-fallback and msg/dispatch paths but left the `DYDO_AGENT` env fast-path on the old descendant-only check — and the codex-hardening dispatch launcher pins `DYDO_AGENT` on every terminal, so a nested foreign-vendor worker inherits it and can still role/release the outer agent. Neither slice review could see it: each was correct in isolation; the hole is in the seam. The DR-035 chain (three rewrites of the same files) left one genuine incoherence — the create-with-body ingress duplicate-mints where the sibling path self-heals — plus reference/comment drift against its own recorded live smoke.

None of the confirmed findings is a known-open exclusion. Filed as issues **#0256–#0265** (found-by inquisition). Report held for Adele's landing sequencing.

---

### Findings → Issues

| # | Sev | Lens | Title | Issue |
|---|-----|------|-------|-------|
| 1 | high | correctness / security | `DYDO_AGENT` env path bypasses the 0250 nearest-host-wins gate — nested foreign-vendor worker can role/release the outer agent | **#0256** |
| 2 | med | correctness | `notion reset --parent-page` archives the REAL board's databases (provision state is project-scoped, not parent-scoped) | **#0257** |
| 3 | med | correctness | Half-resolved shadow promotes conflict-marker residue into a canonical doc and pushes it to Notion (gate ANDs both sentinels) | **#0258** |
| 4 | med | correctness | `DocsPageAdapter` create-with-body read-back guard throws before recording the created page — each retry mints an orphan duplicate | **#0259** |
| 5 | med | coverage | `ArchiveDatabase` + create-with-body `markdown` have no wire-shape test — fake/wire divergence invisible to the suite | **#0261** |
| 6 | med | doc drift | `notion-sync.md` contradicts the recorded DR-035 live smoke (still "pending smoke", folder write shape Notion rejects 400) | **#0260** |
| 7 | low | security | Node-ancestor vendor classification matches `claude`/`codex` anywhere in the command line (fail-closed ownership refusal) | **#0265** |
| 8 | low | coverage | Coverage gaps on identity/launch/sync seams (provenance surfaces, reset wiring, vanished-doc fallback, watchdog resume resolver, ancestry real-walk) | **#0264** |
| 9 | low | dead code | Cross-rewrite dead / dead-in-effect surfaces (TerminalLauncher helpers, `GetCurrentOwnedAgent` re-check, `UnknownBlockIds`, watchdog anchor comment) | **#0263** |
| 10 | low | doc drift | Codex/2.0 doc drift: `dispatch-and-messaging.md` claude-only spawn; `troubleshooting.md` `claude --resume` + removed-audit grep | **#0262** |

The security sweep independently re-found finding 1 (from the `GetSessionContext` env-branch angle) — same defect, folded into #0256. The child-safe-PATCH comment drift and the create-with-body "unconfirmed" staleness (low doc-drift, same root as finding 6) are folded into #0260. The low coverage gaps and low dead-code findings are consolidated into #0264 and #0263 rather than filed one-per to keep the tracker legible; every sub-finding and its evidence is preserved in the issue bodies.

---

### The cross-campaign seams (why this needed a campaign lens)

**Process identity — 0250 × codex launcher × watchdog.** All three touch process ancestry, and the campaign lens is what surfaced #0256: the 0250 fix (`7805e004`) wired `IsOwnedByNearestHostCaller` into the file fallback and msg/dispatch, but not the `DYDO_AGENT` env fast-path (`AgentRegistry.cs:1308-1313`, `:1286-1296`, `:1141`) — and the codex-hardening launcher (`WindowsTerminalLauncher.cs:41`, `LinuxTerminalLauncher.cs:170`) pins `DYDO_AGENT` on every dispatched terminal, which children inherit. The exact 0250 threat (MCP-spawned codex-under-claude) re-enters through env inheritance, on the self-mutating command surface the fix left uncovered. The same seam produced the secondary findings: the node-cmdline classifier over-matches the vendor token anywhere (#0265), the claim-time ancestry walks are pinned only on the happy path (#0264/5), the watchdog dispatch-time anchor was left claude-only with a now-false comment (#0263/5), and `GetCurrentOwnedAgent`'s 0230-era re-check became dead-in-effect once 0250 gated `GetSessionContext` underneath it (#0263/3).

**DR-035 — three rewrites of the same files, is the composition coherent?** Mostly yes, with one real seam: `85674a76` introduced the adapter create path with throw-before-record, `c2aeff8a` fixed the *same* failure mode gracefully in `DocsTreeSync.CreatePageWithBodyAndRecord` (empty base + child-safe PATCH degrade), but the adapter ingress was never brought into line — so the final composition self-heals on one create path and orphan-duplicates on the other (#0259). The shadow gate (#0258) and the reference/comment drift against the recorded smoke (#0260) are the other residue of the three-pass rewrite. The known-open DR-035 convergence gap (reappend-tags-on-write) is correctly excluded; #0260 is specifically the reference doc contradicting recorded results, not the pending behavior.

**Notion reset — new command, old lesson unlearned.** `6d985884` shipped the same day DR-035 parent-scoped the docs-mirror snapshot to prevent scratch-vs-real cross-contamination, yet reset keys its provision state by project only and archives the real board under a `--parent-page` scratch override (#0257) — the identical cross-contamination class, un-applied to the spine. Its destructive primitive is also fake-only at the wire (#0261) and its `--yes`/confirm wiring is untested (#0264/2).

### Gates

Assess-only sweep — the workflow's inquisitor agents read; they did not build or run the suite (Sprint C1 is active in Commands/Services territory; readers-only, no collision). Where a finding turned on test behavior, it was verified by reading the test sources and the code paths they do and don't reach, not by executing. Each finding carries file:line evidence in its issue body.

### Disposition

- Issues **#0256–#0265** created (found-by inquisition), severities as tabled.
- **#0256 (high)** is the one to sequence first — it re-opens the 0250 impersonation class on a surface the fix was believed to have closed, and it invalidates two written claims (the 0250 resolution text and the codex-mcp backlog) that assert the env path is safe.
- Report held for Adele's landing sequencing; verdict + summary messaged to Adele.
