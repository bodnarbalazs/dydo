export const meta = {
  name: 'run-issues',
  description: 'Implement reviewed Linear Issues: loop each Issue through code-writer → independent reviewer until PASS, integrate passed worktree branches serially, audit coordinated work against its reviewed Git Project plan, then hand off durable assimilation.',
  whenToUse: 'Run one autonomous-ready Linear Issue, or a coordinated set of disjoint Issues governed by a reviewed Git Project plan. args = { projectPlan?: string, issues: [{ id, brief }] }. Multiple Issues require projectPlan.',
  phases: [
    { title: 'Intent' },
    { title: 'Implement & review' },
    { title: 'Integrate' },
    { title: 'Audit' },
    { title: 'Assimilate' },
    { title: 'Report' },
  ],
}

// Grounded in the human's in-the-wild experience: up to 4 consecutive natural
// review failures have each surfaced a new real issue. The fifth failure
// escalates rather than letting one Issue consume the whole orchestration run.
const MAX_REVIEW_ROUNDS = 5

const RAISE_HAND_NOTE =
  '\n\nIf the reviewed contract is ambiguous, contradicts the codebase, or you are thrashing on the same root cause across rounds, set raiseHand=true with a reason instead of guessing — a human will step in.'

// A model-bound QA stage returns null when its bound model is unavailable. Retry
// once on the configured default fallback; a second null remains a real no-result.
const FALLBACK_MODEL = 'claude-sonnet-5'

async function agentWithFallback(prompt, opts) {
  const result = await agent(prompt, opts)
  if (result != null) return result
  return agent(prompt, { ...opts, model: FALLBACK_MODEL, label: `${opts.label ?? 'stage'}:fallback` })
}

const CODE_RESULT = {
  type: 'object',
  required: ['summary', 'raiseHand', 'branch', 'worktreePath'],
  additionalProperties: false,
  properties: {
    summary: { type: 'string', description: 'What changed this round and the exact test/coverage outcome.' },
    raiseHand: { type: 'boolean', description: 'true if reviewed intent is ambiguous, contradicts the codebase, or repeated work needs human judgment.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
    branch: { type: ['string', 'null'], description: 'Dedicated-worktree branch, or null for direct single-Issue work in the invoking tree.' },
    worktreePath: { type: 'string', description: 'Root of the working tree edited: `git rev-parse --show-toplevel`.' },
  },
}

const REVIEW_RESULT = {
  type: 'object',
  required: ['pass', 'raiseHand', 'evidence'],
  additionalProperties: false,
  properties: {
    pass: { type: 'boolean', description: 'true only when the Issue implementation is correct, tested, standards-clean, and faithful to reviewed intent.' },
    findings: { type: 'string', description: 'Specific actionable findings when not pass.' },
    evidence: { type: 'string', minLength: 1, description: 'Non-empty independent gate commands and diff facts supporting the verdict.' },
    raiseHand: { type: 'boolean', description: 'true if human judgment is needed rather than another implementation round.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
  },
}

const MERGE_RESULT = {
  type: 'object',
  required: ['merged', 'conflicted', 'baseCommit', 'raiseHand'],
  additionalProperties: false,
  properties: {
    merged: {
      type: 'array',
      description: 'Issues whose branch merge completed without abort, in order, with ancestry verification.',
      items: {
        type: 'object',
        required: ['id', 'verified'],
        additionalProperties: false,
        properties: {
          id: { type: 'string' },
          verified: { type: 'boolean', description: 'true only when `git merge-base --is-ancestor <issue-branch> HEAD` exited 0.' },
        },
      },
    },
    conflicted: {
      type: 'array',
      description: 'Issue branches whose merge required judgment and was aborted intact.',
      items: {
        type: 'object',
        required: ['id', 'reason'],
        additionalProperties: false,
        properties: {
          id: { type: 'string' },
          reason: { type: 'string' },
        },
      },
    },
    baseCommit: { type: 'string', description: 'Invoking-branch HEAD before the first Issue branch merged.' },
    raiseHand: { type: 'boolean', description: 'true if integration as a whole needs human judgment.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
  },
}

const AUDIT_RESULT = {
  type: 'object',
  required: ['pass', 'raiseHand', 'evidence'],
  additionalProperties: false,
  properties: {
    pass: { type: 'boolean', description: 'true only if the integrated Project satisfies its reviewed Git plan as one coherent unit.' },
    findings: { type: 'string', description: 'Verified file:line findings when not pass.' },
    evidence: { type: 'string', minLength: 1, description: 'Non-empty Project-plan acceptance and full-gate evidence supporting the verdict.' },
    raiseHand: { type: 'boolean', description: 'true if human judgment is needed beyond reporting findings.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
  },
}

phase('Intent')
const work = normalizeWork(args)
const issues = work.issues
const ISOLATE = issues.length > 1
log(`${issues.length} reviewed Linear Issue(s): ${issues.map(issue => issue.id).join(', ')}. ${ISOLATE ? 'Native worktree isolation enabled.' : 'Direct single-Issue execution.'}`)

phase('Implement & review')
const results = (await parallel(issues.map(issue => () => runIssue(issue)))).filter(Boolean)

phase('Integrate')
let baseCommit = null
let integrationRaisedHand = false
let integrationReason = null
const toMerge = results.filter(result => result.status === 'passed')
if (ISOLATE && toMerge.length > 0) {
  const merge = await agent(mergePrompt(toMerge), {
    agentType: 'code-writer',
    label: 'integrate',
    phase: 'Integrate',
    schema: MERGE_RESULT,
  })
  baseCommit = merge?.baseCommit ?? null
  integrationRaisedHand = merge?.raiseHand === true
  integrationReason = merge?.reason ?? null
  for (const result of toMerge) {
    const conflict = merge?.conflicted?.find(item => item.id === result.id)
    if (conflict) {
      result.status = 'escalated'
      result.stage = 'integrate'
      result.reason = conflict.reason
      continue
    }
    const claim = merge?.merged?.find(item => item.id === result.id)
    if (claim?.verified) {
      result.merged = true
      continue
    }
    result.status = 'escalated'
    result.stage = 'integrate'
    result.reason = claim ? 'merge claimed but ancestry verification failed' : (merge?.reason ?? 'integration agent did not account for this Issue')
  }
  log(`Integration: ${toMerge.filter(result => result.merged).length}/${toMerge.length} passed Issue branch(es) landed.`)
} else {
  for (const result of toMerge) {
    if (result.branch) {
      result.status = 'escalated'
      result.stage = 'integrate'
      result.reason = `single-Issue work landed on unexpected branch ${result.branch}`
      continue
    }
    result.merged = true
  }
}

phase('Audit')
let auditVerdict, auditFindings, auditEvidence
const fullyIntegrated = !integrationRaisedHand
  && results.length === issues.length
  && results.every(result => result.status === 'passed' && result.merged)
if (!work.projectPlan && fullyIntegrated) {
  auditVerdict = 'not-required'
  auditFindings = 'Atomic Issue acceptance is complete after its independent review; no coordinated Project audit applies.'
} else if (!work.projectPlan) {
  auditVerdict = 'skipped'
  auditFindings = 'The atomic Issue did not pass independent review and land in the invoking tree; acceptance is blocked.'
} else if (fullyIntegrated) {
  const audit = await agentWithFallback(auditPrompt(baseCommit), {
    agentType: 'reviewer',
    label: 'project-audit',
    phase: 'Audit',
    schema: AUDIT_RESULT,
  })
  auditVerdict = audit?.pass && !audit.raiseHand && isNonBlank(audit.evidence) ? 'pass' : 'fail'
  auditFindings = audit
    ? [
        audit.findings,
        audit.pass && !isNonBlank(audit.evidence) ? 'Project audit returned PASS without reproducible evidence.' : null,
        audit.raiseHand && audit.reason ? `Raised hand: ${audit.reason}` : audit.reason,
      ].filter(Boolean).join('\n\n')
    : 'the integrated Project audit did not return a result'
  auditEvidence = audit?.evidence
} else {
  auditVerdict = 'skipped'
  auditFindings = integrationRaisedHand
    ? `Integration raised its hand: ${integrationReason ?? 'human judgment is required.'}`
    : 'Not every planned Issue passed independent review and landed; the integrated Project audit cannot run on a partial result.'
}

phase('Assimilate')
const assimilation = work.projectPlan
  ? {
      required: auditVerdict === 'pass',
      destination: 'dydo/Git',
      instruction: auditVerdict === 'pass'
        ? 'Create the proportionate durable assimilation brief from Issue/review/audit evidence before completing the Linear Project; cover observed friction, adopted knowledge, and deferred follow-ups.'
        : 'Resolve integration or audit findings before writing the final assimilation brief or completing the Linear Project.',
    }
  : {
      required: fullyIntegrated,
      destination: fullyIntegrated ? 'dydo/Git' : null,
      instruction: fullyIntegrated
        ? 'Create a proportionate assimilation brief before completing the Linear Issue. Capture reusable knowledge in dydo/Git and state explicitly when the accepted change produced no durable follow-up.'
        : 'Pass independent review and land the atomic Issue before assimilation or completion.',
    }

phase('Report')
const passed = results.filter(result => result.status === 'passed')
const escalated = results.filter(result => result.status === 'escalated')
log(`Done: ${passed.length} Issue(s) passed independent review and integrated; ${escalated.length} escalated. Project audit: ${auditVerdict}.`)
return {
  passed: passed.map(result => result.id),
  escalated,
  issues: results.map(result => ({
    id: result.id,
    status: result.status,
    round: result.round,
    branch: result.branch ?? null,
    worktreePath: result.worktreePath ?? null,
    merged: result.merged === true,
    reviewEvidence: result.reviewEvidence ?? null,
  })),
  auditVerdict,
  auditFindings,
  auditEvidence,
  integrationRaisedHand,
  integrationReason,
  assimilation,
}

async function runIssue(issue) {
  let feedback = null
  let worktree = {}
  for (let round = 1; round <= MAX_REVIEW_ROUNDS; round++) {
    const codeOpts = {
      agentType: 'code-writer',
      label: `code:${issue.id}#${round}`,
      phase: 'Implement & review',
      schema: CODE_RESULT,
    }
    if (ISOLATE) codeOpts.isolation = 'worktree'
    const code = await agent(codePrompt(issue, feedback, round), codeOpts)
    if (code?.branch) worktree = { branch: code.branch, worktreePath: code.worktreePath }
    if (!code || code.raiseHand)
      return escalate(issue, 'code-writer', round, code?.reason ?? 'code-writer did not return a result', { summary: code?.summary, ...worktree })

    const review = await agentWithFallback(reviewPrompt(issue, code.summary, worktree), {
      agentType: 'reviewer',
      label: `review:${issue.id}#${round}`,
      phase: 'Implement & review',
      schema: REVIEW_RESULT,
    })
    if (!review || review.raiseHand)
      return escalate(issue, 'reviewer', round, review?.reason ?? 'reviewer did not return a result', { findings: review?.findings, reviewEvidence: review?.evidence, ...worktree })
    if (review.pass && !isNonBlank(review.evidence))
      return escalate(issue, 'reviewer', round, 'reviewer returned PASS without non-empty reproducible evidence', { reviewEvidence: review.evidence, ...worktree })
    if (review.pass)
      return { id: issue.id, status: 'passed', round, reviewEvidence: review.evidence, ...worktree }

    feedback = review.findings
  }
  return escalate(issue, 'review-cap', MAX_REVIEW_ROUNDS,
    `${MAX_REVIEW_ROUNDS} consecutive independent reviews did not pass — human judgment is required.`, worktree)
}

function escalate(issue, stage, round, reason, extra = {}) {
  return { id: issue.id, status: 'escalated', stage, round, reason, ...extra }
}

function isNonBlank(value) {
  return typeof value === 'string' && value.trim().length > 0
}

function normalizeWork(value) {
  if (typeof value === 'string') {
    try { value = JSON.parse(value) } catch { throw new Error('run-issues expects args = { projectPlan?: string, issues: [{ id, brief }] }.') }
  }
  if (!value || Array.isArray(value) || typeof value !== 'object')
    throw new Error('run-issues expects args = { projectPlan?: string, issues: [{ id, brief }] }.')
  if (!Array.isArray(value.issues) || value.issues.length === 0)
    throw new Error('run-issues requires a non-empty issues array.')

  const issues = value.issues.map((issue, index) => {
    if (!issue || typeof issue !== 'object' || typeof issue.id !== 'string' || !issue.id.trim() || typeof issue.brief !== 'string' || !issue.brief.trim())
      throw new Error(`run-issues issue ${index + 1} requires non-empty id and brief strings.`)
    return { id: issue.id.trim(), brief: issue.brief.trim() }
  })
  const ids = issues.map(issue => issue.id)
  if (new Set(ids).size !== ids.length) throw new Error('run-issues requires unique Linear Issue identifiers.')

  const projectPlan = typeof value.projectPlan === 'string' && value.projectPlan.trim()
    ? value.projectPlan.trim()
    : null
  if (issues.length > 1 && !projectPlan)
    throw new Error('Coordinated multi-Issue work requires the exact reviewed Git Project plan and governing commit in projectPlan.')
  return { projectPlan, issues }
}

function codePrompt(issue, feedback, round) {
  const modeNote = ISOLATE
    ? '\n\nYou are in a native dedicated worktree for this Linear Issue. Commit only this Issue\'s owned files on that worktree branch and return `git branch --show-current` plus `git rev-parse --show-toplevel` as branch and worktreePath.'
    : '\n\nYou work directly in the invoking tree for this one Issue. Do not create or reuse a branch or worktree and do not commit; return branch: null and `git rev-parse --show-toplevel` as worktreePath.'
  const planNote = work.projectPlan ? `\n\nGoverning reviewed Git Project plan:\n${work.projectPlan}` : ''
  const prior = round === 1 ? '' : `\n\nIndependent review round ${round - 1} failed. Fix exactly these findings:\n${feedback}`
  return `Implement Linear Issue ${issue.id}.\n\nReviewed Issue contract:\n${issue.brief}${planNote}\n\nStay inside the Issue's owned paths. Implement fully, add or adjust tests, and run its exact gates.${modeNote}${prior}${RAISE_HAND_NOTE}`
}

function reviewPrompt(issue, codeSummary, worktree) {
  const whereNote = ISOLATE && worktree?.branch
    ? `\n\nReview branch \`${worktree.branch}\` in worktree \`${worktree.worktreePath}\`; run diffs and gates there, not in the invoking tree.`
    : ''
  const planNote = work.projectPlan ? `\n\nGoverning reviewed Git Project plan:\n${work.projectPlan}` : ''
  return `Independently review Linear Issue ${issue.id}.\n\nReviewed Issue contract:\n${issue.brief}${planNote}\n\nImplementation report:\n${codeSummary}${whereNote}\n\nFollow the reviewer methodology. Verify the actual diff and rerun the Issue's exact gates. PASS only if there are no findings; return reproducible evidence for the Linear Issue. If human judgment is needed, set raiseHand=true.`
}

function mergePrompt(toMerge) {
  const list = toMerge.map((result, index) => `${index + 1}. ${result.id} — branch \`${result.branch}\`, worktree \`${result.worktreePath}\``).join('\n')
  return `Integrate ${toMerge.length} independently reviewed Linear Issue branch(es) into the invoking branch, strictly one at a time in this order:\n\n${list}\n\nWork in the invoking tree and record its HEAD as baseCommit before the first merge. For each Issue, first commit any remaining work on its dedicated branch with \`git -C <worktreePath> add -A && git -C <worktreePath> commit -m "<Issue id>: implementation"\`; do not switch branches or commit that work from the invoking tree. Then merge with \`git merge --no-ff <branch> -m "merge <Issue id>"\` and verify \`git merge-base --is-ancestor <branch> HEAD\`. Record successful merges with the actual verification result. On a non-trivial conflict, abort that merge, preserve the Issue branch, record it under conflicted, and continue. Never rebase, force, or discard Issue work.` + RAISE_HAND_NOTE
}

function auditPrompt(baseCommit) {
  const scope = baseCommit
    ? `\`git diff ${baseCommit}..HEAD\` plus uncommitted changes (\`git status\`, \`git diff HEAD\`)`
    : 'the complete integrated change in the invoking tree, including `git diff HEAD` and untracked files'
  const issueBlock = issues.map(issue => {
    const result = results.find(candidate => candidate.id === issue.id)
    return `### ${issue.id}\n${issue.brief}\n\nIndependent review evidence:\n${result?.reviewEvidence ?? 'MISSING'}`
  }).join('\n\n')
  return `Perform the independent integrated Project audit over ${scope}. Follow references/merge-sprint.md, which now defines the integrated-Project rubric. Verify the entire result against the exact reviewed Git Project plan below, not only the Issue briefs. Every planned Issue has landed; confirm its independent-review evidence and run the plan's full gates. Return file:line findings plus acceptance evidence. PASS means no findings.\n\n## Reviewed Git Project plan\n${work.projectPlan}\n\n## Integrated Linear Issues\n${issueBlock}`
}
