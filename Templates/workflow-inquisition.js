export const meta = {
  name: 'inquisition',
  description: 'Rare integrated audit of a landed Project: inquisitors sweep the whole changed surface lens by lens, each of them refutes its own catch, the reviewer judges the audit with the merge rubric at full scale, and the docs-writer writes the assimilation brief. It runs only after the human confirms it.',
  whenToUse: 'Run when every independently reviewed Linear Issue of a coordinated Project has landed and the human has confirmed this exact run. args = { projectPlan: string, scope: string, issueEvidence: string, confirmed: true }. The confirmed flag carries the human decision to spend this run: propose the scope and the cost, ask, and pass back the answer you were given. Without it the workflow refuses to start. The exact reviewed Git Project plan with its governing commit, the complete integrated scope, and the Linear Issue review evidence are all required.',
  phases: [
    { title: 'Contract' },
    { title: 'Sweep' },
    { title: 'Verify' },
    { title: 'Judge' },
    { title: 'Assimilate' },
    { title: 'Report' },
  ],
}

const ARGS = 'inquisition expects args = { projectPlan: string, scope: string, issueEvidence: string, confirmed: true }.'

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
          location: { type: 'string', description: 'file:line, or the exact area.' },
          severity: { type: 'string', enum: ['high', 'medium', 'low'] },
          rationale: { type: 'string', description: 'What breaks, and why that earns this severity.' },
        },
      },
    },
  },
}

const VERIFICATION = {
  type: 'object',
  required: ['verdict', 'evidence'],
  additionalProperties: false,
  properties: {
    verdict: { type: 'string', enum: ['confirmed', 'plausible', 'refuted'] },
    evidence: { type: 'string', description: 'The evidence that decided it; for plausible, the missing fact.' },
  },
}

const REVIEW_BLOCK = {
  type: 'object',
  required: ['rubric', 'reviewer', 'candidate', 'base', 'verdict', 'gates', 'findings', 'resolutions'],
  additionalProperties: false,
  properties: {
    rubric: { type: 'string', enum: ['merge'] },
    reviewer: { type: 'string', description: 'Reviewer label and model.' },
    candidate: { type: 'string', description: 'The integrated ref and its SHA.' },
    base: { type: 'string', description: 'The SHA the merged tree is diffed against.' },
    verdict: { type: 'string', enum: ['PASS', 'FAIL'] },
    gates: { type: 'string', minLength: 1, description: 'Every gate rerun on the integrated state, with its result.' },
    findings: { type: 'string', description: 'file:line → consequence → correction, or None.' },
    resolutions: {
      type: 'array',
      description: 'Step 6 of the merge rubric: one entry for every finding handed over, resolved on evidence the judge verified.',
      items: {
        type: 'object',
        required: ['id', 'verdict', 'evidence'],
        additionalProperties: false,
        properties: {
          id: { type: 'integer', description: 'The id of the finding being resolved.' },
          verdict: { type: 'string', enum: ['confirmed', 'plausible', 'refuted'] },
          evidence: { type: 'string', description: 'The evidence the judge verified; for plausible, the missing fact.' },
        },
      },
    },
  },
}

const ASSIMILATION = {
  type: 'object',
  required: ['path', 'brief', 'noneHeadings'],
  additionalProperties: false,
  properties: {
    path: { type: 'string', description: 'Where the brief was written under dydo/project/migrations/.' },
    brief: { type: 'string', minLength: 1, description: 'The assimilation brief itself.' },
    noneHeadings: { type: 'string', description: 'Every heading the evidence could not support, or None.' },
  },
}

const LENSES = [
  { key: 'correctness', hunt: 'Hunt for correctness defects: wrong or inverted conditions, off-by-one errors, null and undefined paths, swallowed failures, races, and unhandled edge cases.' },
  { key: 'coverage', hunt: 'Hunt for coverage gaps: behavior no trustworthy test proves, untested error paths and seams, and assertions that would still pass with the implementation broken.' },
  { key: 'security', hunt: 'Hunt for security defects: missing boundary validation, injection, path traversal, secrets, broken authorization, and unsafe deserialization.' },
  { key: 'dead-code', hunt: 'Hunt for dead or orphaned code: unreachable paths, unused exports and fields, stale compatibility behavior, and retirement left half-finished.' },
  { key: 'doc-drift', hunt: 'Hunt for documentation drift: docs, comments, help text, templates, or durable knowledge that contradict the integrated implementation or the reviewed plan.' },
  { key: 'seams', hunt: 'Hunt at the seams between Issues: shared-file collisions, broken assumptions, contradictory logic, lost hunks, doubled code, and integration left half-done.' },
]

phase('Contract')
const audit = confirmedRun(args)
log(`Human-confirmed inquisition over ${audit.scope}, against the reviewed Git Project plan at its governing commit.`)

phase('Sweep')
const perLens = await pipeline(
  LENSES,
  lens => agent(
    `You are an inquisitor on a human-confirmed inquisition. Your assigned lens is ${lens.key}; use only it, because sibling inquisitors carry the others. Your purpose is to catch what got through, not to prove zero defects.\n\n${lens.hunt}\n\nComplete integrated scope:\n${audit.scope}\n\nReviewed Git Project plan and governing commit:\n${audit.projectPlan}\n\nLinear Issue review evidence:\n${audit.issueEvidence}\n\nReturn up to 8 findings from the ${lens.key} lens, strongest first, each with an exact location, a severity of high, medium or low, and the rationale naming what breaks. Clear your evidence bar on every one: a smell, a preference, or a hypothetical is not a finding, and settled work stays settled. A plan-acceptance failure is a finding even when every Issue review passed. An empty list is a real result.`,
    { agentType: 'inquisitor', label: `sweep:${lens.key}`, phase: 'Sweep', schema: FINDINGS }),
  (found, lens) => {
    if (!found) return [{
      title: `The ${lens.key} lens returned no result`,
      location: `inquisition:${lens.key}`,
      severity: 'high',
      rationale: 'A required lens did not complete, so this inquisition cannot claim it swept the whole surface.',
      lens: lens.key,
      verification: { verdict: 'plausible', evidence: 'No structured sweep result came back.' },
    }]
    return parallel(found.findings.map(finding => () =>
      agent(
        `You are an inquisitor verifying one finding from the ${lens.key} lens of this inquisition. Start refuted and argue against the claim; let the evidence overturn you.\n\nFinding: ${finding.title}\nLocation: ${finding.location}\nClaimed severity: ${finding.severity}\nClaim: ${finding.rationale}\n\nComplete integrated scope:\n${audit.scope}\n\nReviewed Git Project plan and governing commit:\n${audit.projectPlan}\n\nReturn confirmed only when the repository proves the claim, plausible only when unavailable state would settle it — then name the missing fact — otherwise refuted, with the evidence that decided it. The reviewer judging this inquisition re-resolves your verdict, so give it evidence it can check rather than a conclusion it must trust.`,
        { agentType: 'inquisitor', label: `verify:${lens.key}`, phase: 'Verify', schema: VERIFICATION })
        .then(verification => ({
          ...finding,
          lens: lens.key,
          verification: verification ?? { verdict: 'plausible', evidence: 'Verification returned no result, so the claim stands unrefuted.' },
        }))))
  }
)

phase('Verify')
const findings = perLens.filter(Boolean).flat().filter(Boolean).map((finding, index) => ({ id: index + 1, ...finding }))
log(`${findings.length} findings swept and verified across ${LENSES.length} lenses; the judge re-resolves every one.`)

phase('Judge')
const reviewBlock = await agent(
  `You are the reviewer judging this inquisition. Read .claude/skills/reviewer/resources/merge.md and work the merge rubric at full scale over the entire integrated state; this is the Project's final merge review, so its acceptance criteria are proved here or nowhere.\n\nComplete integrated scope:\n${audit.scope}\n\nReviewed Git Project plan and governing commit:\n${audit.projectPlan}\n\nLinear Issue review evidence:\n${audit.issueEvidence}\n\nReported findings, each already refuted or upheld once by the inquisitor that raised it:\n${JSON.stringify(findings, null, 2)}\n\nRead the integrated diff yourself, account for every Issue review, prove every acceptance criterion, and rerun the plan's gates on the merged tree. Then work step 6: resolve every reported finding by its id to confirmed, plausible or refuted on evidence you verify yourself, defaulting to refuted and naming the missing fact where you reach for plausible. The inquisitors' verifications are input to re-check, not a result to accept, and a finding you leave unresolved blocks this gate. Return the review block — rubric merge, your label and model, candidate and base SHA, the verdict, every gate rerun with its result, findings as file:line → consequence → correction — with your resolutions. PASS means every criterion and gate is proven and no finding survives your own resolution; there is no PASS with notes.`,
  { agentType: 'reviewer', label: 'judge', phase: 'Judge', schema: REVIEW_BLOCK })

const resolutions = reviewBlock?.resolutions ?? []
const confirmed = resolutions.filter(resolution => resolution.verdict === 'confirmed')
const plausible = resolutions.filter(resolution => resolution.verdict === 'plausible')
const everyFindingJudged = findings.every(finding => resolutions.some(resolution => resolution.id === finding.id))
if (!everyFindingJudged) log('The judge left reported findings unresolved; a partial judgement cannot pass this gate.')

const gate = reviewBlock?.verdict === 'PASS' && isNonBlank(reviewBlock.gates)
  && everyFindingJudged && confirmed.length === 0 && plausible.length === 0 ? 'PASS' : 'FAIL'

phase('Assimilate')
const assimilation = await agent(
  `You are the docs-writer. Write this inquisition's assimilation brief under dydo/project/migrations/, using the headings its predecessors carry: What changed, Integrated proof, Observed friction, Acceptance boundary, Deferred follow-ups, Related. Every claim needs its witness in the evidence below; where a heading has none, write None rather than filling it, and invent nothing. The brief is durable knowledge in dydo/Git, and Linear links it as acceptance evidence rather than copying it.\n\nReviewed Git Project plan and governing commit:\n${audit.projectPlan}\n\nLinear Issue review evidence:\n${audit.issueEvidence}\n\nInquisition gate: ${gate}\nThe judge's review block and resolutions:\n${JSON.stringify(reviewBlock ?? 'The judge returned no review block.', null, 2)}\nReported findings, by id:\n${JSON.stringify(findings, null, 2)}`,
  { agentType: 'docs-writer', label: 'assimilation', phase: 'Assimilate', schema: ASSIMILATION })

phase('Report')
const projectAcceptanceReady = gate === 'PASS' && isNonBlank(assimilation?.brief)
const high = confirmed.filter(resolution => findings.find(finding => finding.id === resolution.id)?.severity === 'high').length
log(`Inquisition ${gate}: ${findings.length} findings swept; the judge confirmed ${confirmed.length} (${high} high), left ${plausible.length} plausible, refuted the rest. Project acceptance: ${projectAcceptanceReady ? 'ready' : 'blocked'}.`)

return {
  gate,
  projectPlan: audit.projectPlan,
  findings,
  reviewBlock: reviewBlock ?? 'The judge returned no review block.',
  confirmed,
  plausible,
  assimilation: assimilation ?? 'The assimilation stage returned no brief.',
  projectAcceptanceReady,
}

function confirmedRun(value) {
  if (typeof value === 'string') {
    try { value = JSON.parse(value) } catch { throw new Error(ARGS) }
  }
  if (!value || Array.isArray(value) || typeof value !== 'object')
    throw new Error(ARGS)
  if (value.confirmed !== true)
    throw new Error('The inquisition is the human gate: it runs only on an explicit confirmation, carried back here as confirmed: true. Propose the scope and the cost, ask, and re-invoke with the answer you were given. Never set confirmed yourself.')
  if (typeof value.projectPlan !== 'string' || !value.projectPlan.trim())
    throw new Error('An inquisition requires the exact reviewed Git Project plan and its governing commit in projectPlan.')
  if (typeof value.scope !== 'string' || !value.scope.trim())
    throw new Error('An inquisition requires the complete integrated scope it is to sweep.')
  if (typeof value.issueEvidence !== 'string' || !value.issueEvidence.trim())
    throw new Error('An inquisition requires the independent Linear Issue review evidence in issueEvidence.')
  return {
    projectPlan: value.projectPlan.trim(),
    scope: value.scope.trim(),
    issueEvidence: value.issueEvidence.trim(),
  }
}

function isNonBlank(value) {
  return typeof value === 'string' && value.trim().length > 0
}
