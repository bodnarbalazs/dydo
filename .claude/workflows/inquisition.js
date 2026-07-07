export const meta = {
  name: 'inquisition',
  description: 'Campaign-end QA gate: sweep the changed/target code across multiple lenses (correctness, test coverage gaps, security, dead code, doc drift), adversarially verify each finding, and synthesize a report. The dydo 2.0 replacement for the old audit-derived `dydo inquisition`.',
  whenToUse: 'Run after a campaign\'s sprints land, to make sure no bugs slipped in and the work is covered and well-tested. args = optional scope string (default: the changes on the current branch).',
  phases: [
    { title: 'Sweep' },
    { title: 'Verify' },
    { title: 'Report' },
  ],
}

const FINDINGS = {
  type: 'object',
  required: ['findings'],
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        required: ['title', 'location', 'severity', 'rationale'],
        properties: {
          title: { type: 'string' },
          location: { type: 'string', description: 'file:line or area' },
          severity: { type: 'string', enum: ['high', 'medium', 'low'] },
          rationale: { type: 'string', description: 'Why this is a problem; for coverage gaps, what is untested and the risk.' },
        },
      },
    },
  },
}

const VERDICT = {
  type: 'object',
  required: ['verdict'],
  properties: {
    verdict: { type: 'string', enum: ['confirmed', 'plausible', 'refuted'] },
    evidence: { type: 'string', description: 'The specific code/fact that confirms or refutes it.' },
  },
}

// The lenses. "coverage" is the inquisition's signature concern: what is NOT tested,
// which the per-change code-review never checks.
const LENSES = [
  { key: 'correctness', prompt: 'Hunt for correctness bugs: wrong/inverted conditions, off-by-one, null/undefined paths, swallowed errors, race conditions, edge cases the code does not handle.' },
  { key: 'coverage', prompt: 'Hunt for TEST COVERAGE GAPS: behavior with no test, error paths/edge cases untested, code above the project test tier with no test, assertions that would pass even if the code were broken. Report each gap as a finding.' },
  { key: 'security', prompt: 'Hunt for security issues: missing validation at boundaries, injection, path traversal, secrets in code/logs, broken auth/permission checks, unsafe deserialization.' },
  { key: 'deadcode', prompt: 'Hunt for dead/orphaned code, unreachable branches, unused exports/fields, and stale references to removed features.' },
  { key: 'docdrift', prompt: 'Hunt for documentation drift: docs/comments/help-text/templates that describe behavior the code no longer has, or commands/features that were removed.' },
]

// The inquisitor stages are bound to the strong tier's model; agent() returns null when it is
// unavailable — the canonical case is Fable hitting its weekly spend cap, which the API blocks
// with no retry and no native fallback (issue #214). Retrying ONCE on a declared fallback keeps a
// model outage from silently voiding the whole QA sweep. Hardcoded (not read from dydo.json's
// models.fallback) because the workflow sandbox has no filesystem/config access — keep it in step
// with ConfigFactory.CreateDefaultModels().Fallback.
const FALLBACK_MODEL = 'claude-sonnet-5'

async function agentWithFallback(prompt, opts) {
  const result = await agent(prompt, opts)
  if (result != null) return result
  return agent(prompt, { ...opts, model: FALLBACK_MODEL, label: `${opts.label ?? 'stage'}:fallback` })
}

phase('Sweep')
const scope = typeof args === 'string' && args.trim()
  ? args.trim()
  : 'the changes on the current git branch (run `git diff main...HEAD` and `git diff HEAD` to see them)'
log(`Inquisition over: ${scope}`)

// Each lens sweeps, then each of its findings is adversarially verified — pipelined,
// so a lens's findings verify as soon as that sweep returns (no barrier).
const perLens = await pipeline(
  LENSES,
  lens => agentWithFallback(
    `You are auditing ${scope}.\n\n${lens.prompt}\n\nReturn up to 8 concrete findings with file:line locations. Only real, nameable problems — no speculation.`,
    { agentType: 'inquisitor', label: `sweep:${lens.key}`, phase: 'Sweep', schema: FINDINGS }),
  (found, lens) => parallel((found?.findings ?? []).map(f => () =>
    agentWithFallback(
      `Adversarially verify this ${lens.key} finding from an audit of ${scope}.\n\nFinding: ${f.title}\nLocation: ${f.location}\nClaim: ${f.rationale}\n\nDefault to "refuted" unless you can confirm it from the actual code (cite the line). "plausible" only if realistic but state-dependent.`,
      { agentType: 'inquisitor', label: `verify:${lens.key}`, phase: 'Verify', schema: VERDICT })
      .then(v => ({ ...f, lens: lens.key, verdict: v?.verdict ?? 'refuted', evidence: v?.evidence }))))
)

phase('Report')
const all = perLens.filter(Boolean).flat().filter(Boolean)
const confirmed = all.filter(f => f.verdict === 'confirmed')
const plausible = all.filter(f => f.verdict === 'plausible')
const bySeverity = s => confirmed.filter(f => f.severity === s).length

log(`Inquisition complete: ${confirmed.length} confirmed (${bySeverity('high')} high), ${plausible.length} plausible, ${all.length - confirmed.length - plausible.length} refuted.`)

const gate = bySeverity('high') === 0 ? 'PASS' : 'FAIL'
return {
  gate, // PASS only if no confirmed high-severity findings remain
  confirmed,
  plausible,
  coverageGaps: confirmed.concat(plausible).filter(f => f.lens === 'coverage'),
}
