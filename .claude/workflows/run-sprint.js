export const meta = {
  name: 'run-sprint',
  description: 'Implement a sprint: for each disjoint slice, loop code-writer → reviewer until the review passes, escalating to the human after 5 failed review rounds or whenever a worker raises its hand.',
  whenToUse: 'Run a sprint of pre-sliced, disjoint implementation work with automated code→review loops (dydo 2.0 Sprint-4 flagship). Pass args = array of slice briefs (strings) or {name, brief} objects.',
  phases: [
    { title: 'Slice' },
    { title: 'Implement & review' },
    { title: 'Report' },
  ],
}

// Grounded in the human's in-the-wild experience: up to 4 consecutive *natural*
// review fails observed (each surfacing a new real issue), so 5 gives headroom
// before escalating instead of burning tokens on a stuck slice.
const MAX_REVIEW_ROUNDS = 5

// Worker structured outputs. Both carry a `raiseHand` circuit-breaker so any
// worker can proactively pull the human in mid-loop, independent of the cap.
const CODE_RESULT = {
  type: 'object',
  required: ['summary', 'raiseHand'],
  additionalProperties: false,
  properties: {
    summary: { type: 'string', description: 'What you implemented or changed this round, and the test/coverage outcome.' },
    raiseHand: { type: 'boolean', description: 'true if the spec is ambiguous, contradicts the codebase, or you are thrashing — human judgment needed.' },
    reason: { type: 'string', description: 'If raiseHand is true, why human eyes are needed.' },
  },
}

const REVIEW_RESULT = {
  type: 'object',
  required: ['pass', 'raiseHand'],
  additionalProperties: false,
  properties: {
    pass: { type: 'boolean', description: 'true ONLY if the change is correct, tested, and standards-clean. PASS means perfect — no "pass with notes".' },
    findings: { type: 'string', description: 'Specific, actionable findings when not pass.' },
    raiseHand: { type: 'boolean', description: 'true if something needs human judgment rather than another code round.' },
    reason: { type: 'string', description: 'If raiseHand is true, why.' },
  },
}

phase('Slice')
const slices = normalizeSlices(args)
log(`Sprint: ${slices.length} slice(s) — ${slices.map(s => s.name).join(', ')}.`)

phase('Implement & review')
// Slices have disjoint file sets, so they run in parallel; each code-writer is
// isolated in its own git worktree to avoid build-lock and cross-contamination.
const results = (await parallel(slices.map(slice => () => runSlice(slice)))).filter(Boolean)

phase('Report')
const passed = results.filter(r => r.status === 'passed')
const escalated = results.filter(r => r.status === 'escalated')
log(`Done: ${passed.length} passed, ${escalated.length} escalated to the human.`)
return {
  passed: passed.map(r => r.name),
  escalated, // each: { name, stage, round, reason, findings? } — surfaces to you
}

// One slice: loop code → review until pass, the cap, or a raised hand.
async function runSlice(slice) {
  let feedback = null
  for (let round = 1; round <= MAX_REVIEW_ROUNDS; round++) {
    const code = await agent(codePrompt(slice, feedback, round), {
      agentType: 'code-writer',
      isolation: 'worktree',
      label: `code:${slice.name}#${round}`,
      phase: 'Implement & review',
      schema: CODE_RESULT,
    })
    if (!code || code.raiseHand)
      return escalate(slice, 'code-writer', round, code?.reason ?? 'code-writer did not return a result', { summary: code?.summary })

    const review = await agent(reviewPrompt(slice, code.summary), {
      agentType: 'reviewer',
      label: `review:${slice.name}#${round}`,
      phase: 'Implement & review',
      schema: REVIEW_RESULT,
    })
    if (!review || review.raiseHand)
      return escalate(slice, 'reviewer', round, review?.reason ?? 'reviewer did not return a result', { findings: review?.findings })
    if (review.pass)
      return { name: slice.name, status: 'passed', round }

    feedback = review.findings // the next code round fixes exactly these
  }
  return escalate(slice, 'review-cap', MAX_REVIEW_ROUNDS,
    `${MAX_REVIEW_ROUNDS} consecutive review rounds did not pass — human eyes needed.`)
}

function escalate(slice, stage, round, reason, extra = {}) {
  return { name: slice.name, status: 'escalated', stage, round, reason, ...extra }
}

function normalizeSlices(a) {
  if (typeof a === 'string') return [{ name: 'slice-1', brief: a }]
  if (!Array.isArray(a) || a.length === 0)
    throw new Error('run-sprint expects args = a non-empty array of slice briefs (strings) or {name, brief} objects.')
  return a.map((s, i) => typeof s === 'string'
    ? { name: `slice-${i + 1}`, brief: s }
    : { name: s.name ?? `slice-${i + 1}`, brief: s.brief })
}

const RAISE_HAND_NOTE =
  '\n\nIf the brief is ambiguous, contradicts the codebase, or you find yourself thrashing on the same root cause across rounds, set raiseHand=true with a reason instead of guessing — a human will step in.'

function codePrompt(slice, feedback, round) {
  const base = `You are implementing sprint slice "${slice.name}".\n\nBrief:\n${slice.brief}\n\nImplement it fully, add or adjust tests, and run the worktree-isolated test runner + coverage gate before finishing. Return a structured result.`
  const roundNote = round === 1 ? '' :
    `\n\nThis is round ${round}. A prior review FAILED — address these findings specifically:\n${feedback}`
  return base + roundNote + RAISE_HAND_NOTE
}

function reviewPrompt(slice, codeSummary) {
  return `Review the implementation of sprint slice "${slice.name}".\n\nBrief:\n${slice.brief}\n\nThe code-writer reports:\n${codeSummary}\n\nReview strictly per your reviewer methodology — PASS only if correct, tested, and standards-clean (no "pass with notes"; PASS means perfect). Run the tests + coverage gate. Return a structured verdict; if not pass, make findings specific and actionable. If something needs human judgment rather than another code round, set raiseHand=true.`
}
