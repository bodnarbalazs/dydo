export const meta = {
  name: 'inquisition',
  description: 'Project-level integrated QA gate: sweep the complete changed surface across independent lenses, adversarially verify every finding, judge it against the reviewed Git Project plan, and synthesize durable assimilation input.',
  whenToUse: 'Run after all independently reviewed Linear Issues for a coordinated Project have landed. args = { projectPlan: string, scope: string, issueEvidence: string }. The exact reviewed plan, governing commit, and Issue-review evidence are required.',
  phases: [
    { title: 'Contract' },
    { title: 'Sweep' },
    { title: 'Verify' },
    { title: 'Judge' },
    { title: 'Assimilate' },
    { title: 'Report' },
  ],
}

const FINDINGS = {
  type: 'object',
  required: ['findings'],
  additionalProperties: false,
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        required: ['title', 'location', 'severity', 'rationale'],
        additionalProperties: false,
        properties: {
          title: { type: 'string' },
          location: { type: 'string', description: 'file:line or exact area' },
          severity: { type: 'string', enum: ['high', 'medium', 'low'] },
          rationale: { type: 'string', description: 'Why this violates correctness, the reviewed plan, or a Project gate.' },
        },
      },
    },
  },
}

const VERDICT = {
  type: 'object',
  required: ['verdict', 'evidence'],
  additionalProperties: false,
  properties: {
    verdict: { type: 'string', enum: ['confirmed', 'plausible', 'refuted'] },
    evidence: { type: 'string', description: 'Specific code, plan, or gate evidence that confirms or refutes the finding.' },
  },
}

const ACCEPTANCE = {
  type: 'object',
  required: ['pass', 'findings', 'evidence'],
  additionalProperties: false,
  properties: {
    pass: { type: 'boolean', description: 'true only when every Project-plan acceptance criterion and full gate passes with no verified finding.' },
    findings: { type: 'string', description: 'Any acceptance or gate failure, with exact evidence.' },
    evidence: { type: 'string', minLength: 1, description: 'Non-empty criterion-by-criterion and command-level proof supporting the verdict.' },
  },
}

const ASSIMILATION = {
  type: 'object',
  required: ['brief'],
  additionalProperties: false,
  properties: {
    brief: { type: 'string', minLength: 1, description: 'A concise durable assimilation brief with Observed friction, Adopted knowledge, and Deferred follow-ups. State None where evidence supplies none.' },
  },
}

const LENSES = [
  { key: 'correctness', prompt: 'Hunt for correctness bugs: wrong or inverted conditions, off-by-one errors, null/undefined paths, swallowed failures, races, and unhandled edge cases.' },
  { key: 'coverage', prompt: 'Hunt for test-coverage gaps: Project behavior with no meaningful test, untested error paths or seams, and assertions that would pass when the implementation is broken.' },
  { key: 'security', prompt: 'Hunt for security issues: missing boundary validation, injection, path traversal, secrets, broken authorization, and unsafe deserialization.' },
  { key: 'deadcode', prompt: 'Hunt for dead or orphaned code, unreachable paths, unused exports or fields, stale compatibility behavior, and incomplete retirement of replaced surfaces.' },
  { key: 'docdrift', prompt: 'Hunt for documentation drift: docs, comments, help text, templates, or durable knowledge that contradict the integrated implementation or reviewed Project plan.' },
  { key: 'seams', prompt: 'Hunt specifically at cross-Issue seams: shared-file collisions, broken assumptions, contradictory logic, lost hunks, doubled code, and incomplete integration.' },
]

const FALLBACK_MODEL = 'claude-sonnet-5'

async function agentWithFallback(prompt, opts) {
  const result = await agent(prompt, opts)
  if (result != null) return result
  return agent(prompt, { ...opts, model: FALLBACK_MODEL, label: `${opts.label ?? 'stage'}:fallback` })
}

phase('Contract')
const audit = normalizeAudit(args)
log(`Integrated Project audit over ${audit.scope}; exact reviewed Git Project plan supplied.`)

phase('Sweep')
const perLens = await pipeline(
  LENSES,
  lens => agentWithFallback(
    `Audit the complete integrated Project scope below.\n\nScope:\n${audit.scope}\n\nReviewed Git Project plan and governing commit:\n${audit.projectPlan}\n\nLinear Issue/review evidence:\n${audit.issueEvidence}\n\n${lens.prompt}\n\nReturn up to 8 concrete findings with exact locations. Only real, nameable problems; no speculation. A plan-acceptance failure is a finding even when individual Issue briefs passed.`,
    { agentType: 'inquisitor', label: `sweep:${lens.key}`, phase: 'Sweep', schema: FINDINGS }),
  (found, lens) => {
    if (!found) return [{
      title: `${lens.key} sweep returned no result`,
      location: `inquisition:${lens.key}`,
      severity: 'high',
      rationale: 'The required independent lens did not complete, so the integrated audit cannot claim a clean sweep.',
      lens: lens.key,
      verdict: 'plausible',
      evidence: 'No structured sweep result was returned after the fallback attempt.',
    }]
    return parallel(found.findings.map(finding => () =>
      agentWithFallback(
        `Adversarially verify this ${lens.key} finding against the integrated Project and its reviewed plan.\n\nScope:\n${audit.scope}\n\nReviewed Git Project plan:\n${audit.projectPlan}\n\nFinding: ${finding.title}\nLocation: ${finding.location}\nClaim: ${finding.rationale}\n\nDefault to refuted unless actual code, the plan, or a gate confirms it. Use plausible only for a realistic state-dependent risk and name the missing fact.`,
        { agentType: 'inquisitor', label: `verify:${lens.key}`, phase: 'Verify', schema: VERDICT })
        .then(verdict => verdict
          ? { ...finding, lens: lens.key, verdict: verdict.verdict, evidence: verdict.evidence }
          : { ...finding, lens: lens.key, verdict: 'plausible', evidence: 'Independent verification returned no result after the fallback attempt.' })))
  }
)

phase('Verify')
const all = perLens.filter(Boolean).flat().filter(Boolean)
const confirmed = all.filter(finding => finding.verdict === 'confirmed')
const plausible = all.filter(finding => finding.verdict === 'plausible')

phase('Judge')
const acceptance = await agentWithFallback(
  `Independently judge the complete integrated Project against the exact reviewed Git Project plan.\n\nScope:\n${audit.scope}\n\nReviewed Git Project plan and governing commit:\n${audit.projectPlan}\n\nLinear Issue/review evidence:\n${audit.issueEvidence}\n\nVerified audit findings:\n${JSON.stringify({ confirmed, plausible }, null, 2)}\n\nRead the actual integrated diff, verify every acceptance criterion, account for every Issue review, and run the plan's full gates. PASS means every criterion and gate is proven and there are no confirmed or unresolved plausible findings. Return criterion-by-criterion and command-level evidence.`,
  { agentType: 'reviewer', label: 'project-acceptance', phase: 'Judge', schema: ACCEPTANCE })

const gate = acceptance?.pass && isNonBlank(acceptance.evidence) && confirmed.length === 0 && plausible.length === 0 ? 'PASS' : 'FAIL'

phase('Assimilate')
const assimilation = await agentWithFallback(
  `Draft durable assimilation input for the reviewed Git Project plan below. Do not invent observations. Summarize only evidence from the independently reviewed Linear Issues, integrated audit, and acceptance verdict. Use exactly these headings: Observed friction, Adopted knowledge, Deferred follow-ups. This brief belongs in dydo/Git; Linear should link it as acceptance evidence rather than copy its durable content.\n\nReviewed Git Project plan:\n${audit.projectPlan}\n\nIssue/review evidence:\n${audit.issueEvidence}\n\nAudit gate: ${gate}\nAcceptance evidence:\n${acceptance?.evidence ?? 'No acceptance result.'}\nAcceptance findings:\n${acceptance?.findings ?? 'None.'}\nVerified findings:\n${JSON.stringify({ confirmed, plausible }, null, 2)}`,
  { agentType: 'docs-writer', label: 'assimilation', phase: 'Assimilate', schema: ASSIMILATION })
const assimilationComplete = isNonBlank(assimilation?.brief)
const projectAcceptanceReady = gate === 'PASS' && assimilationComplete

phase('Report')
const bySeverity = severity => confirmed.filter(finding => finding.severity === severity).length
log(`Integrated Project audit ${gate}: ${confirmed.length} confirmed (${bySeverity('high')} high), ${plausible.length} plausible, ${all.length - confirmed.length - plausible.length} refuted. Project acceptance: ${projectAcceptanceReady ? 'ready' : 'blocked'}.`)

return {
  gate,
  projectPlan: audit.projectPlan,
  confirmed,
  plausible,
  coverageGaps: confirmed.concat(plausible).filter(finding => finding.lens === 'coverage'),
  acceptanceEvidence: acceptance?.evidence,
  acceptanceFindings: acceptance?.findings,
  assimilationBrief: assimilation?.brief ?? 'Assimilation stage did not return a brief.',
  assimilationComplete,
  projectAcceptanceReady,
}

function normalizeAudit(value) {
  if (typeof value === 'string') {
    try { value = JSON.parse(value) } catch { throw new Error('inquisition expects args = { projectPlan: string, scope: string, issueEvidence: string }.') }
  }
  if (!value || Array.isArray(value) || typeof value !== 'object')
    throw new Error('inquisition expects args = { projectPlan: string, scope: string, issueEvidence: string }.')
  if (typeof value.projectPlan !== 'string' || !value.projectPlan.trim())
    throw new Error('A Project-level integrated audit requires the exact reviewed Git Project plan and governing commit in projectPlan.')
  if (typeof value.scope !== 'string' || !value.scope.trim())
    throw new Error('A Project-level integrated audit requires a non-empty complete merged scope.')
  if (typeof value.issueEvidence !== 'string' || !value.issueEvidence.trim())
    throw new Error('A Project-level integrated audit requires non-empty independent Linear Issue review evidence.')
  return {
    projectPlan: value.projectPlan.trim(),
    scope: value.scope.trim(),
    issueEvidence: value.issueEvidence.trim(),
  }
}

function isNonBlank(value) {
  return typeof value === 'string' && value.trim().length > 0
}
